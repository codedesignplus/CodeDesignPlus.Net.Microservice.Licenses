using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

[EventKey<OrderAggregate>(1, "OrderPaidAndReadyForProvisioningDomainEvent")]
public class OrderPaidAndReadyForProvisioningDomainEvent(
    Guid aggregateId,
    Tenant tenantDetail,
    LicenseTenant license,
    Guid buyerId,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    public Tenant TenantDetail { get; } = tenantDetail;
    public LicenseTenant License { get; } = license;
    public Guid BuyerId { get; } = buyerId;

    /// <summary>
    /// Los paquetes de la orden con su periodo, para que ms-tenants los combine con la licencia (pendings/739). Vacío si la
    /// orden no trae paquetes: los que la copropiedad ya tenía siguen como estaban.
    /// </summary>
    public List<PackageTenant> Packages { get; init; } = [];

    /// <summary>
    /// Lo que necesita la factura electrónica de la compra, con una línea por la licencia y otra por cada paquete
    /// (pendings/661, 739). Nulo en lo que republica la conciliación.
    /// </summary>
    public SubscriptionBilling? Billing { get; init; }

    /// <summary>
    /// Crea el evento.
    /// </summary>
    /// <param name="aggregateId">La orden.</param>
    /// <param name="tenantDetail">La copropiedad que se crea.</param>
    /// <param name="license">La licencia con su periodo.</param>
    /// <param name="buyerId">Quien pagó.</param>
    /// <param name="packages">Los paquetes comprados con la licencia.</param>
    /// <param name="billing">Lo que se factura.</param>
    /// <returns>El evento.</returns>
    public static OrderPaidAndReadyForProvisioningDomainEvent Create(Guid aggregateId, Tenant tenantDetail, LicenseTenant license, Guid buyerId, List<PackageTenant>? packages = null, SubscriptionBilling? billing = null)
    {
        return new OrderPaidAndReadyForProvisioningDomainEvent(aggregateId, tenantDetail, license, buyerId) { Packages = packages ?? [], Billing = billing };
    }
}
