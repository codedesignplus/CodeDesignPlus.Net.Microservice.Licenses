using CodeDesignPlus.Net.Hangfire.Abstractions;
using CodeDesignPlus.Net.Hangfire.Abstractions.Attributes;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Security.Abstractions;
using CodeDesignPlus.Net.Security.Abstractions.Options;
using Hangfire;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Jobs;

/// <summary>
/// Job diario que avisa de los vencimientos de las licencias: los días previos configurados, el día que vence y el día que
/// termina la gracia (pendings/736), con la gracia de <c>Security:GraceDays</c> (pendings/737).
/// </summary>
/// <remarks>
/// Publica <c>SubscriptionExpiringDomainEvent</c> y nada más: ms-licenses es de CodeDesignPlus y no sabe quién administra
/// una copropiedad. Kappali resuelve los destinatarios y envía el correo y la campana.
/// <para>
/// Las fechas son las del calendario de cada copropiedad. Cada aviso deja su constancia con un identificador derivado de
/// la copropiedad, el fin del periodo pagado y el día del aviso (regla 23): correr dos veces no repite nada, y una
/// renovación o un cambio de licencia, al mover el fin del periodo, cortan los avisos del anterior. Una copropiedad que
/// falla no impide avisar a las demás, pero el job falla al final para que el panel de Hangfire lo muestre.
/// </para>
/// <para>
/// Solo avisa a las copropiedades activas del directorio de ms-tenants: una dada de baja conserva sus órdenes durante el
/// plazo de la baja diferida (pendings/078), y avisarle que renueve sería escribirle a quien ya se fue (pendings/735).
/// </para>
/// </remarks>
[RecurringJobOptions("0 11 * * *", jobId: "subscription-expiration-job")]
public class SubscriptionExpirationJob(
    IOrderRepository orders,
    ISubscriptionNoticeRepository notices,
    IPubSub pubsub,
    ITenantDirectory tenants,
    IOptions<SubscriptionOptions> options,
    IOptions<SecurityOptions> security,
    ILogger<SubscriptionExpirationJob> logger
) : IRecurrentJob
{
    /// <inheritdoc/>
    [DisableConcurrentExecution(timeoutInSeconds: 10 * 60)]
    public Task ExecuteAsync(IJobCancellationToken jobCancellationToken) => RunAsync(SystemClock.Instance.GetCurrentInstant(), jobCancellationToken.ShutdownToken);

    /// <summary>
    /// Emite los avisos que tocan en ese instante.
    /// </summary>
    /// <param name="now">El instante de la corrida.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La tarea.</returns>
    /// <exception cref="InvalidOperationException">Si alguna copropiedad falló.</exception>
    public async Task RunAsync(Instant now, CancellationToken cancellationToken)
    {
        var failed = 0;

        var active = (await tenants.GetActiveTenantsAsync(cancellationToken)).ToHashSet();

        failed += await RepublishUnconfirmedAsync(active, now, cancellationToken);

        var paid = await orders.FindAllPaidAsync(cancellationToken);
        var withPackages = (await orders.FindAllPaidWithPackagesAsync(cancellationToken) ?? []).ToLookup(x => x.TenantDetail.Id);
        var emitted = 0;

        foreach (var tenantOrders in paid.GroupBy(x => x.TenantDetail.Id).Where(x => active.Contains(x.Key)))
        {
            try
            {
                emitted += await NotifyAsync(tenantOrders, withPackages[tenantOrders.Key], now, cancellationToken) ? 1 : 0;
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogError(ex, "Failed to evaluate the expiration notices of tenant {TenantId}.", tenantOrders.Key);
            }
        }

        if (emitted > 0)
        {
            logger.LogInformation("Subscription expiration notices emitted: {Emitted}.", emitted);
        }

        // Sin esto, una copropiedad caída sale en verde en el panel (regla 23).
        if (failed > 0)
        {
            throw new InvalidOperationException($"The expiration notices failed for {failed} tenant(s).");
        }
    }

    private async Task<bool> NotifyAsync(IEnumerable<OrderAggregate> tenantOrders, IEnumerable<OrderAggregate> packageOrders, Instant now, CancellationToken cancellationToken)
    {
        var subscription = SubscriptionTimeline.From(tenantOrders);

        if (subscription is null)
        {
            return false;
        }

        var settings = options.Value;
        // La gracia es la de Security, la misma que aplica el middleware de licencias (pendings/737): un solo número.
        var graceDays = security.Value.GraceDays;
        var notice = ExpirationNotices.Due(subscription.DaysRemaining(now), subscription.Standing(now, graceDays), subscription.DaysSinceGraceEnded(now, graceDays), settings.ReminderDays, graceDays, settings.NoticeCatchUpDays);

        if (notice is null)
        {
            return false;
        }

        // Los paquetes vigentes vencen o se suspenden con la licencia: el aviso los nombra (pendings/739).
        var packages = PackageTerm.Current(PackageTerm.PaidLines(packageOrders), now).Select(x => x.Line.Name).Distinct().ToList();

        var aggregate = SubscriptionNoticeAggregate.Create(subscription, notice, now, graceDays, Guid.Empty, packages);

        // Persistir, publicar, confirmar (regla 23): si ya existía, ese aviso ya salió o lo reintenta la constancia.
        if (!await notices.CreateIfAbsentAsync(aggregate, cancellationToken))
        {
            return false;
        }

        await pubsub.PublishAsync(aggregate.GetAndClearEvents(), cancellationToken);
        await notices.MarkPublishedAsync(aggregate.Id, now, cancellationToken);

        return true;
    }

    private async Task<int> RepublishUnconfirmedAsync(HashSet<Guid> active, Instant now, CancellationToken cancellationToken)
    {
        var failed = 0;

        // El de una copropiedad dada de baja queda sin publicar: si la restauran, sale entonces.
        foreach (var notice in (await notices.FindUnpublishedAsync(cancellationToken)).Where(x => active.Contains(x.Tenant)))
        {
            try
            {
                // El mismo identificador de evento: quien avisa lo usa para no repetir el aviso si el anterior sí salió.
                await pubsub.PublishAsync(notice.ToEvent(), cancellationToken);
                await notices.MarkPublishedAsync(notice.Id, now, cancellationToken);
            }
            catch (Exception ex)
            {
                failed++;
                logger.LogError(ex, "Failed to republish the expiration notice {NoticeId} of tenant {TenantId}.", notice.Id, notice.Tenant);
            }
        }

        return failed;
    }
}
