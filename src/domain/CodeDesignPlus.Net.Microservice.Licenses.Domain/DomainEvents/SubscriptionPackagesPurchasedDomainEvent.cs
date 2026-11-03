using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

/// <summary>
/// Se pagó la compra o la ampliación de un paquete de una copropiedad que ya existe (pendings/739).
/// </summary>
/// <remarks>
/// ms-tenants suma con él el paquete a los atributos efectivos de la copropiedad; si amplía otro, la línea nueva reemplaza
/// a la anterior desde su inicio. Lleva lo que necesitan la nota crédito sobre lo no usado de la línea reemplazada
/// (<see cref="CreditedPeriods"/>) y la factura del paquete (<see cref="Billing"/>), pendings/661 y 663.
/// </remarks>
/// <param name="aggregateId">La orden del paquete.</param>
/// <param name="tenantDetail">La copropiedad, con sus datos fiscales.</param>
/// <param name="packages">El paquete comprado, con su periodo.</param>
/// <param name="creditedPeriods">Lo no usado de la línea que se amplía; vacío en una compra.</param>
/// <param name="billing">Lo que se factura.</param>
/// <param name="buyerId">Quien pagó.</param>
/// <param name="eventId">El identificador del evento.</param>
/// <param name="occurredAt">Cuándo ocurrió.</param>
/// <param name="metadata">Los metadatos.</param>
[EventKey<OrderAggregate>(1, "SubscriptionPackagesPurchasedDomainEvent")]
public class SubscriptionPackagesPurchasedDomainEvent(
    Guid aggregateId,
    Tenant tenantDetail,
    List<PackageTenant> packages,
    List<CreditedPeriod> creditedPeriods,
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
    /// El paquete comprado, con su periodo.
    /// </summary>
    public List<PackageTenant> Packages { get; } = packages;

    /// <summary>
    /// Lo no usado de la línea que se amplía.
    /// </summary>
    public List<CreditedPeriod> CreditedPeriods { get; } = creditedPeriods;

    /// <summary>
    /// Lo que se factura.
    /// </summary>
    public SubscriptionBilling Billing { get; } = billing;

    /// <summary>
    /// Quien pagó.
    /// </summary>
    public Guid BuyerId { get; } = buyerId;

    /// <summary>
    /// Crea el evento.
    /// </summary>
    /// <param name="aggregateId">La orden.</param>
    /// <param name="tenantDetail">La copropiedad.</param>
    /// <param name="packages">El paquete comprado.</param>
    /// <param name="creditedPeriods">Lo no usado de la línea que se amplía.</param>
    /// <param name="billing">Lo que se factura.</param>
    /// <param name="buyerId">Quien pagó.</param>
    /// <returns>El evento.</returns>
    public static SubscriptionPackagesPurchasedDomainEvent Create(Guid aggregateId, Tenant tenantDetail, List<PackageTenant> packages, List<CreditedPeriod> creditedPeriods, SubscriptionBilling billing, Guid buyerId)
    {
        return new SubscriptionPackagesPurchasedDomainEvent(aggregateId, tenantDetail, packages, creditedPeriods, billing, buyerId);
    }
}
