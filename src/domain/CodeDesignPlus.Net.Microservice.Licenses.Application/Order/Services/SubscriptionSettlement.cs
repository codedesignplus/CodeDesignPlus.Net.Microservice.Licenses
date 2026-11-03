using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// Lo que sigue al resultado del pago de una renovación o de un cambio de licencia, fuera de la propia orden
/// (pendings/735, 738).
/// </summary>
public interface ISubscriptionSettlement
{
    /// <summary>
    /// El pago se aprobó: inactiva las órdenes cuyo periodo se acreditó y lleva al saldo a favor lo que sobra del crédito;
    /// si la orden había sido reemplazada, lleva al saldo a favor lo pagado.
    /// </summary>
    /// <param name="order">La orden pagada.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La tarea.</returns>
    Task SettleAsync(OrderAggregate order, CancellationToken cancellationToken);

    /// <summary>
    /// El pago no se aprobó: devuelve al saldo a favor lo que la orden había apartado.
    /// </summary>
    /// <param name="order">La orden.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La tarea.</returns>
    Task ReleaseAsync(OrderAggregate order, CancellationToken cancellationToken);
}

/// <summary>
/// Lo que sigue al resultado del pago de una renovación o de un cambio de licencia (pendings/735, 738).
/// </summary>
/// <remarks>
/// Todo es idempotente: los movimientos del saldo tienen un identificador derivado de la orden y del motivo, e inactivar
/// una orden ya inactiva no cambia nada. Una reentrega del pago puede volver a pasar por aquí sin repetir nada.
/// </remarks>
public class SubscriptionSettlement(IOrderRepository orders, ICreditBalanceRepository creditBalances, ILogger<SubscriptionSettlement> logger) : ISubscriptionSettlement
{
    /// <inheritdoc/>
    public async Task SettleAsync(OrderAggregate order, CancellationToken cancellationToken)
    {
        var now = SystemClock.Instance.GetCurrentInstant();

        if (order.SupersededBy is not null)
        {
            // Otra orden la reemplazó y su enlace se pagó igual: no cambia la licencia, y lo pagado no se queda en la
            // plataforma por un servicio que no se presta.
            logger.LogWarning("Order {OrderId} was paid after being superseded by {ReplacedBy}: {Amount} goes to the credit balance of tenant {TenantId}.", order.Id, order.SupersededBy, order.AmountToCharge.Amount, order.TenantDetail.Id);

            await ApplyAsync(order, CreditMovement.ForOrder(order.Id, CreditMovementKind.UnappliedPayment, order.AmountToCharge, now), cancellationToken);

            return;
        }

        if (order.Type != OrderType.Change && order.Type != OrderType.Package)
        {
            return;
        }

        // Solo se inactivan las órdenes cuya licencia se acreditó: lo acreditado de un paquete lo reemplaza su línea nueva, y
        // la orden de esa línea puede seguir pagando la licencia (pendings/739).
        await orders.DeactivateAsync([.. (order.CreditedPeriods ?? []).Where(x => x.LineId is null).Select(x => x.OrderId).Distinct()], order.Buyer.BuyerId, cancellationToken);

        // Del crédito por lo no prestado, lo que no alcanzó a gastarse en el precio de lo nuevo.
        var creditApplied = order.Total - order.AmountToCharge - (order.CreditBalanceApplied ?? Money.Zero(order.Total.Currency));
        var leftover = order.ProrationCredit - creditApplied;

        await ApplyAsync(order, CreditMovement.ForOrder(order.Id, CreditMovementKind.CreditNote, leftover, now), cancellationToken);
    }

    /// <inheritdoc/>
    public Task ReleaseAsync(OrderAggregate order, CancellationToken cancellationToken)
    {
        if (order.CreditBalanceApplied is not { Amount: > 0 } reserved)
        {
            return Task.CompletedTask;
        }

        return ApplyAsync(order, CreditMovement.ForOrder(order.Id, CreditMovementKind.Released, reserved, SystemClock.Instance.GetCurrentInstant()), cancellationToken);
    }

    private async Task ApplyAsync(OrderAggregate order, CreditMovement movement, CancellationToken cancellationToken)
    {
        if (movement.Amount.Amount <= 0)
        {
            return;
        }

        var tenant = order.TenantDetail.Id;

        await creditBalances.UpdateWithRetryAsync(
            async () => await creditBalances.FindAsync<CreditBalanceAggregate>(tenant, tenant, cancellationToken) ?? CreditBalanceAggregate.Create(tenant, movement.Amount.Currency),
            balance => balance.Apply(movement),
            cancellationToken);
    }
}
