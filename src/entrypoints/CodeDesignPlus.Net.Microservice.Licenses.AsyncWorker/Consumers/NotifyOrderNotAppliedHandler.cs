using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;
using System.Globalization;
using CodeDesignPlus.Net.ValueObjects.Financial;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Consumers;

/// <summary>
/// Avisa a los administradores de la plataforma de que una orden pagada no se pudo aplicar (Kappali pendings/737, paso 6):
/// el pago se aprobó y la licencia o el paquete no quedó en el tenant, así que alguien tiene que revisarlo. Va a su campana
/// y, si está configurado, por correo al buzón de operaciones con el asunto marcado como importante.
/// </summary>
/// <remarks>
/// <para>
/// Lo consume este micro porque es el dueño de la orden: el aviso lleva el tenant, el tipo de orden, lo que se pagó (en el
/// payload, sin formatear) y el motivo leyéndola. Va a la campana del rol de la plataforma, sin tenant, porque quien lo atiende trabaja fuera de los
/// tenants. El correo no va a las personas del rol (no existe un contrato para listar quién tiene un rol), sino al buzón
/// de operaciones de <see cref="PlatformNoticeOptions.OperationsMailbox"/>; sin buzón solo suena la campana.
/// </para>
/// <para>
/// Los ids del aviso y del correo salen del evento: una reentrega no los cambia. Si la orden no aparece, avisa con lo que
/// trae el evento en vez de perder el aviso. El correo dice lo pagado formateado, como los demás correos de este servicio:
/// el correo no tiene un frontend que lo formatee.
/// </para>
/// </remarks>
[QueueName<OrderAggregate>("NotifyOrderNotAppliedHandler")]
public class NotifyOrderNotAppliedHandler(
    IOrderRepository orders,
    IInboxGrpc inbox,
    IPubSub pubSub,
    ICurrencyGrpc currencies,
    IOptions<PlatformNoticeOptions> options,
    ILogger<NotifyOrderNotAppliedHandler> logger
) : IEventHandler<OrderProvisioningFailedDomainEvent>
{
    private static readonly Guid EmailNamespace = Guid.Parse("4d7b2e91-0c3a-4f58-a6e2-9b1d5c737e06");

    /// <inheritdoc/>
    public async Task HandleAsync(OrderProvisioningFailedDomainEvent data, CancellationToken token)
    {
        var role = options.Value.PlatformAdministratorRole;

        // Sin el rol el aviso no llegaría a nadie: falla para que el mensaje se reintente y se vea en el log.
        if (role == Guid.Empty)
        {
            throw new InvalidOperationException($"'{PlatformNoticeOptions.Section}:{nameof(PlatformNoticeOptions.PlatformAdministratorRole)}' is not configured: the paid order {data.AggregateId} that was not applied cannot be notified.");
        }

        var order = await orders.FindAsync<OrderAggregate>(data.AggregateId, token);

        string body;
        object payload;

        if (order is null)
        {
            logger.LogError("The paid order {OrderId} that was not applied does not exist: the notice carries only the event.", data.AggregateId);

            body = OrderNotAppliedText.MessageWithoutOrder(data.AggregateId, data.StepName, data.Error);
            payload = new { orderId = data.AggregateId, buyerId = data.BuyerId, stepName = data.StepName, reason = data.Error, failedAt = data.OccurredAt };
        }
        else
        {
            var paid = order.AmountToCharge;

            // El monto va solo en el payload: lo formatea el frontend con la moneda y el locale del usuario.
            body = OrderNotAppliedText.Message(order.TenantDetail.Name, order.Type, data.StepName, data.Error);
            payload = new
            {
                orderId = data.AggregateId,
                buyerId = data.BuyerId,
                stepName = data.StepName,
                reason = data.Error,
                failedAt = data.OccurredAt,
                tenantId = order.TenantDetail.Id,
                tenantName = order.TenantDetail.Name,
                orderType = (int)order.Type,
                amountPaid = paid.Amount,
                currency = paid.Currency,
            };
        }

        await inbox.NotifyRolesAsync(
            [role.ToString()],
            PlatformNotificationKinds.LicenseOrderNotApplied,
            OrderNotAppliedText.Title,
            body,
            OrderNotAppliedText.Module,
            data.AggregateId.ToString(),
            payload,
            // Sin tenant: el administrador de la plataforma lo lee fuera de todos.
            Guid.Empty,
            // Lo manda el sistema, no el comprador: este micro transversal firma así lo automático (como sus jobs).
            Guid.Empty,
            data.OccurredAt.ToDateTimeOffset(),
            NotificationIds.For(data.EventId, PlatformNotificationKinds.LicenseOrderNotApplied),
            token);

        logger.LogWarning("Paid order {OrderId} was not applied ({Step}): the platform administrators were notified.", data.AggregateId, data.StepName);

        await EmailAsync(data, order, token);
    }

    /// <summary>
    /// Manda el correo al buzón de operaciones, si hay uno configurado. Va después de la campana: si la moneda no se puede
    /// leer, el mensaje se reintenta y la campana, con el mismo id, no se repite.
    /// </summary>
    private async Task EmailAsync(OrderProvisioningFailedDomainEvent data, OrderAggregate? order, CancellationToken token)
    {
        var mailbox = options.Value.OperationsMailbox?.Trim();

        if (string.IsNullOrEmpty(mailbox))
        {
            logger.LogWarning("'{Section}:{Option}' is not configured: the paid order {OrderId} that was not applied is notified only in the inbox.", PlatformNoticeOptions.Section, nameof(PlatformNoticeOptions.OperationsMailbox), data.AggregateId);

            return;
        }

        var subject = OrderNotAppliedText.EmailSubject(order?.TenantDetail.Name ?? data.AggregateId.ToString());
        var content = order is null
            ? OrderNotAppliedText.EmailContentWithoutOrder(data.AggregateId, data.StepName, data.Error)
            : OrderNotAppliedText.EmailContent(order.TenantDetail.Name, order.Type, data.AggregateId, await AmountAsync(order.AmountToCharge, token), data.StepName, data.Error);
        var variables = new Dictionary<string, string>
        {
            ["subject"] = subject,
            ["title"] = OrderNotAppliedText.Title,
            ["content"] = content,
            ["organization_name"] = order?.TenantDetail.Name ?? string.Empty,
            ["current_year"] = data.OccurredAt.InUtc().Year.ToString(CultureInfo.InvariantCulture),
        };

        // El id sale del evento y del buzón: una reentrega publica el mismo correo, no uno nuevo.
        var id = DeterministicGuid.Create(EmailNamespace, data.EventId.ToString(), mailbox);
        // Sin tenant: el correo es de la plataforma, como la campana.
        var email = new SendEmailDomainEvent(id, OrderNotAppliedText.EmailTemplate, [mailbox], [], [], variables, [], Guid.Empty, eventId: id);

        await pubSub.PublishAsync(email, token);

        logger.LogInformation("Paid order {OrderId} that was not applied was emailed to the operations mailbox.", data.AggregateId);
    }

    /// <summary>
    /// Lo pagado como lo leen los correos de este servicio: «$ 142.800,00».
    /// </summary>
    private async Task<string> AmountAsync(Money paid, CancellationToken token)
    {
        var currency = await currencies.GetCurrencyAsync(code: paid.Currency, cancellationToken: token);

        return ReceiptText.Amount(paid, currency);
    }
}
