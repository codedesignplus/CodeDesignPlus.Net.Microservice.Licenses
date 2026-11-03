using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

/// <summary>
/// Se pagó la renovación de la licencia de una copropiedad que ya existe (pendings/735).
/// </summary>
/// <remarks>
/// Es distinto de <see cref="OrderPaidAndReadyForProvisioningDomainEvent"/>, que crea copropiedades. ms-tenants alarga
/// con él la vigencia de la licencia del tenant. Lleva además lo que necesita la factura electrónica (pendings/661), para
/// que quien facture no tenga que leer la orden.
/// </remarks>
/// <param name="aggregateId">La orden de renovación.</param>
/// <param name="tenantDetail">La copropiedad, con sus datos fiscales: es el adquiriente de la factura.</param>
/// <param name="license">La licencia con el periodo renovado.</param>
/// <param name="billing">Lo que se factura.</param>
/// <param name="buyerId">Quien pagó.</param>
/// <param name="eventId">El identificador del evento.</param>
/// <param name="occurredAt">Cuándo ocurrió.</param>
/// <param name="metadata">Los metadatos.</param>
[EventKey<OrderAggregate>(1, "SubscriptionRenewedDomainEvent")]
public class SubscriptionRenewedDomainEvent(
    Guid aggregateId,
    Tenant tenantDetail,
    LicenseTenant license,
    SubscriptionBilling billing,
    Guid buyerId,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    /// <summary>
    /// La copropiedad.
    /// </summary>
    public Tenant TenantDetail { get; } = tenantDetail;

    /// <summary>
    /// La licencia con el periodo renovado.
    /// </summary>
    public LicenseTenant License { get; } = license;

    /// <summary>
    /// Lo que se factura.
    /// </summary>
    public SubscriptionBilling Billing { get; } = billing;

    /// <summary>
    /// Quien pagó.
    /// </summary>
    public Guid BuyerId { get; } = buyerId;

    /// <summary>
    /// Los paquetes de la orden con su periodo, para que ms-tenants los combine con la licencia (pendings/739). Vacío si la
    /// orden no trae paquetes: los que la copropiedad ya tenía siguen como estaban.
    /// </summary>
    public List<PackageTenant> Packages { get; init; } = [];

    /// <summary>
    /// Crea el evento.
    /// </summary>
    /// <param name="aggregateId">La orden.</param>
    /// <param name="tenantDetail">La copropiedad.</param>
    /// <param name="license">La licencia con el periodo renovado.</param>
    /// <param name="billing">Lo que se factura.</param>
    /// <param name="buyerId">Quien pagó.</param>
    /// <param name="packages">Los paquetes renovados con la licencia.</param>
    /// <returns>El evento.</returns>
    public static SubscriptionRenewedDomainEvent Create(Guid aggregateId, Tenant tenantDetail, LicenseTenant license, SubscriptionBilling billing, Guid buyerId, List<PackageTenant>? packages = null)
    {
        return new SubscriptionRenewedDomainEvent(aggregateId, tenantDetail, license, billing, buyerId) { Packages = packages ?? [] };
    }
}
