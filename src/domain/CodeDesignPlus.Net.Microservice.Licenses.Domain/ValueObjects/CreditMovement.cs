using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Un movimiento del saldo a favor de una copropiedad (pendings/738).
/// </summary>
/// <remarks>
/// El identificador se deriva de la orden y del motivo (<see cref="ForOrder"/>): reaplicar el mismo movimiento, por una
/// reentrega del pago o un reintento del formulario, no lo duplica.
/// </remarks>
/// <param name="Id">El identificador, derivado de la orden y del motivo.</param>
/// <param name="Kind">Por qué se movió.</param>
/// <param name="Amount">Cuánto, siempre positivo: el motivo dice si entra o sale.</param>
/// <param name="OrderId">La orden que lo originó.</param>
/// <param name="OccurredAt">Cuándo.</param>
public sealed record CreditMovement(Guid Id, CreditMovementKind Kind, Money Amount, Guid OrderId, Instant OccurredAt)
{
    private static readonly Guid Namespace = Guid.Parse("6f0c2a9e-3b1d-4f57-9c0a-7385a1e2d738");

    /// <summary>
    /// Si el movimiento resta del saldo.
    /// </summary>
    public bool IsDebit => Kind == CreditMovementKind.AppliedToOrder;

    /// <summary>
    /// El movimiento de una orden por un motivo, con su identificador derivado.
    /// </summary>
    /// <param name="orderId">La orden.</param>
    /// <param name="kind">El motivo.</param>
    /// <param name="amount">Cuánto, positivo.</param>
    /// <param name="occurredAt">Cuándo.</param>
    /// <returns>El movimiento.</returns>
    public static CreditMovement ForOrder(Guid orderId, CreditMovementKind kind, Money amount, Instant occurredAt) =>
        new(DeterministicGuid.Create(Namespace, orderId.ToString(), kind.ToString()), kind, amount, orderId, occurredAt);
}
