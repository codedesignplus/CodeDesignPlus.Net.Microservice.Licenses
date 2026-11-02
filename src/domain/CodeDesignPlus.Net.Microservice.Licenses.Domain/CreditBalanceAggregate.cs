using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

/// <summary>
/// El saldo a favor de una copropiedad con ExcellenceForge, con el historial de sus movimientos (pendings/738).
/// </summary>
/// <remarks>
/// Lo crea la nota crédito de un cambio de licencia (lo no prestado de la anterior que no alcanzó a gastarse en la nueva) y
/// lo consumen las órdenes siguientes como anticipo. Quedarse con él sería registrar como ingreso un servicio no prestado
/// (experto contable, 2026-11-02). Hay un documento por copropiedad y su identificador es la copropiedad.
/// <para>
/// Lo escriben a la vez el formulario que aparta saldo para una orden y el pago que lo aplica o lo libera, así que se
/// versiona (<see cref="IVersionedAggregate"/>). No se purga con la copropiedad, igual que sus órdenes: es parte de la
/// venta.
/// </para>
/// </remarks>
/// <param name="id">La copropiedad.</param>
public class CreditBalanceAggregate(Guid id) : AggregateRoot(id), IVersionedAggregate
{
    /// <summary>
    /// La versión del documento guardado: la sube el repositorio en cada escritura.
    /// </summary>
    public long Version { get; private set; }

    /// <summary>
    /// El saldo disponible, en unidades mínimas.
    /// </summary>
    public Money Balance { get; private set; } = null!;

    /// <summary>
    /// Los movimientos, del más antiguo al más reciente.
    /// </summary>
    public List<CreditMovement> Movements { get; private set; } = [];

    /// <summary>
    /// Crea el saldo vacío de una copropiedad.
    /// </summary>
    /// <param name="tenant">La copropiedad, que es también el identificador.</param>
    /// <param name="currency">La moneda en que se le factura.</param>
    /// <returns>El saldo, todavía sin guardar.</returns>
    public static CreditBalanceAggregate Create(Guid tenant, string currency)
    {
        DomainGuard.GuidIsEmpty(tenant, Errors.IdTenantIsRequired);

        return new CreditBalanceAggregate(tenant)
        {
            Tenant = tenant,
            Balance = Money.Zero(currency),
            IsActive = true,
            CreatedAt = SystemClock.Instance.GetCurrentInstant()
        };
    }

    /// <summary>
    /// Aplica un movimiento.
    /// </summary>
    /// <param name="movement">El movimiento.</param>
    /// <returns><c>false</c> si ya estaba aplicado: reaplicarlo no cambia nada.</returns>
    /// <exception cref="CodeDesignPlus.Net.Exceptions.CodeDesignPlusException">Si es una salida mayor que el saldo.</exception>
    public bool Apply(CreditMovement movement)
    {
        DomainGuard.IsNull(movement, Errors.CreditMovementIsRequired);
        DomainGuard.IsTrue(movement.Amount.Amount < 0, Errors.CreditCannotBeNegative);

        if (Movements.Any(x => x.Id == movement.Id))
        {
            return false;
        }

        if (movement.Amount.Amount == 0)
        {
            return false;
        }

        if (movement.IsDebit)
        {
            DomainGuard.IsTrue(movement.Amount.Amount > Balance.Amount, Errors.InsufficientCreditBalance);

            Balance -= movement.Amount;
        }
        else
        {
            Balance += movement.Amount;
        }

        Movements.Add(movement);
        UpdatedAt = movement.OccurredAt;

        return true;
    }
}
