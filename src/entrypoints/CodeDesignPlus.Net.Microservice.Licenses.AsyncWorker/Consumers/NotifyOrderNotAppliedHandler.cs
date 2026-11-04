using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Consumers;

/// <summary>
/// Avisa a los administradores de la plataforma de que una orden pagada no se pudo aplicar (Kappali pendings/737, paso 6):
/// el pago se aprobó y la licencia o el paquete no quedó en el tenant, así que alguien tiene que revisarlo.
/// </summary>
/// <remarks>
/// <para>
/// Lo consume este micro porque es el dueño de la orden: el aviso lleva el tenant, el tipo de orden, lo que se pagó y el
/// motivo leyéndola. Va a la campana del rol de la plataforma, sin tenant, porque quien lo atiende trabaja fuera de los
/// tenants. No hay correo: no existe un contrato para listar quién tiene un rol, y el rol vive en el directorio de
/// identidades, no en este micro.
/// </para>
/// <para>
/// El id del aviso sale del evento: una reentrega no lo repite. Si la orden no aparece, avisa con lo que trae el evento
/// en vez de perder el aviso.
/// </para>
/// </remarks>
[QueueName<OrderAggregate>("NotifyOrderNotAppliedHandler")]
public class NotifyOrderNotAppliedHandler(
    IOrderRepository orders,
    IInboxGrpc inbox,
    ICurrencyGrpc currencies,
    IOptions<PlatformNoticeOptions> options,
    ILogger<NotifyOrderNotAppliedHandler> logger
) : IEventHandler<OrderProvisioningFailedDomainEvent>
{
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
            var currency = await currencies.GetCurrencyAsync(code: paid.Currency, cancellationToken: token);

            body = OrderNotAppliedText.Message(order.TenantDetail.Name, order.Type, ReceiptText.Amount(paid, currency), data.StepName, data.Error);
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
    }
}
