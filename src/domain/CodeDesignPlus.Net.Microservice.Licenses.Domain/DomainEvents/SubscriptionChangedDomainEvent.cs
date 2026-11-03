using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

/// <summary>
/// Se pagó el cambio de la licencia de una copropiedad por una mayor (pendings/738).
/// </summary>
/// <remarks>
/// ms-tenants aplica con él la licencia nueva, con sus módulos y atributos y el periodo que empieza hoy. Lleva además lo
/// que necesitan la nota crédito sobre las facturas anteriores (<see cref="CreditedPeriods"/>) y la factura de la licencia
/// nueva por el precio completo con el anticipo aplicado (<see cref="Billing"/>), pendings/661 y 663.
/// </remarks>
/// <param name="aggregateId">La orden del cambio.</param>
/// <param name="tenantDetail">La copropiedad, con sus datos fiscales: es el adquiriente de la factura.</param>
/// <param name="license">La licencia nueva con su periodo.</param>
/// <param name="previousLicenseId">La licencia que se reemplaza.</param>
/// <param name="creditedPeriods">Lo no prestado de cada orden anterior: una nota crédito por cada una.</param>
/// <param name="billing">Lo que se factura.</param>
/// <param name="buyerId">Quien pagó.</param>
/// <param name="eventId">El identificador del evento.</param>
/// <param name="occurredAt">Cuándo ocurrió.</param>
/// <param name="metadata">Los metadatos.</param>
[EventKey<OrderAggregate>(1, "SubscriptionChangedDomainEvent")]
public class SubscriptionChangedDomainEvent(
    Guid aggregateId,
    Tenant tenantDetail,
    LicenseTenant license,
    Guid previousLicenseId,
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
    /// La licencia nueva con su periodo.
    /// </summary>
    public LicenseTenant License { get; } = license;

    /// <summary>
    /// La licencia que se reemplaza.
    /// </summary>
    public Guid PreviousLicenseId { get; } = previousLicenseId;

    /// <summary>
    /// Lo no prestado de cada orden anterior.
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
    /// Los paquetes de la orden con su periodo, para que ms-tenants los combine con la licencia (pendings/739). Vacío si la
    /// orden no trae paquetes: los que la copropiedad ya tenía siguen como estaban.
    /// </summary>
    public List<PackageTenant> Packages { get; init; } = [];

    /// <summary>
    /// Crea el evento.
    /// </summary>
    /// <param name="aggregateId">La orden.</param>
    /// <param name="tenantDetail">La copropiedad.</param>
    /// <param name="license">La licencia nueva con su periodo.</param>
    /// <param name="previousLicenseId">La licencia que se reemplaza.</param>
    /// <param name="creditedPeriods">Lo no prestado de cada orden anterior.</param>
    /// <param name="billing">Lo que se factura.</param>
    /// <param name="buyerId">Quien pagó.</param>
    /// <param name="packages">Los paquetes recotizados al periodo nuevo, si la licencia es anual.</param>
    /// <returns>El evento.</returns>
    public static SubscriptionChangedDomainEvent Create(Guid aggregateId, Tenant tenantDetail, LicenseTenant license, Guid previousLicenseId, List<CreditedPeriod> creditedPeriods, SubscriptionBilling billing, Guid buyerId, List<PackageTenant>? packages = null)
    {
        return new SubscriptionChangedDomainEvent(aggregateId, tenantDetail, license, previousLicenseId, creditedPeriods, billing, buyerId) { Packages = packages ?? [] };
    }
}
