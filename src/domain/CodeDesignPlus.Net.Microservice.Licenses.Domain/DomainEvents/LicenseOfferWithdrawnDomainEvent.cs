namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

/// <summary>
/// Se le retiró a una copropiedad la licencia privada que se le había ofrecido y que no alcanzó a tomar: ya no la puede
/// elegir al cambiar de licencia (pendings/464).
/// </summary>
/// <remarks>
/// Sale al quitar la copropiedad de la lista de la licencia, al desactivarla o al borrarla mientras se le ofrecía. Retirar la
/// oferta a quien ya la tomó o tiene un pago en curso para tomarla no se permite, así que este evento nunca habla de una
/// licencia que la copropiedad tiene. ms-licenses no sabe a quién avisar: quien consuma el evento resuelve los
/// destinatarios, como con <see cref="LicenseOfferedDomainEvent"/>.
/// </remarks>
/// <param name="aggregateId">La licencia.</param>
/// <param name="licenseName">El nombre de la licencia.</param>
/// <param name="tenant">La copropiedad a la que se le retira.</param>
/// <param name="tenantName">El nombre de la copropiedad.</param>
/// <param name="withdrawnBy">Quién la retiró.</param>
/// <param name="eventId">El identificador del evento.</param>
/// <param name="occurredAt">Cuándo se retiró.</param>
/// <param name="metadata">Los metadatos.</param>
[EventKey<LicenseAggregate>(1, "LicenseOfferWithdrawnDomainEvent")]
public class LicenseOfferWithdrawnDomainEvent(
    Guid aggregateId,
    string licenseName,
    Guid tenant,
    string tenantName,
    Guid withdrawnBy,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    /// <summary>
    /// El nombre de la licencia.
    /// </summary>
    public string LicenseName { get; } = licenseName;

    /// <summary>
    /// La copropiedad a la que se le retira.
    /// </summary>
    public Guid Tenant { get; } = tenant;

    /// <summary>
    /// El nombre de la copropiedad.
    /// </summary>
    public string TenantName { get; } = tenantName;

    /// <summary>
    /// Quién la retiró.
    /// </summary>
    public Guid WithdrawnBy { get; } = withdrawnBy;

    /// <summary>
    /// Crea el evento.
    /// </summary>
    /// <param name="aggregateId">La licencia.</param>
    /// <param name="licenseName">El nombre de la licencia.</param>
    /// <param name="tenant">La copropiedad.</param>
    /// <param name="tenantName">El nombre de la copropiedad.</param>
    /// <param name="withdrawnBy">Quién la retiró.</param>
    /// <returns>El evento.</returns>
    public static LicenseOfferWithdrawnDomainEvent Create(Guid aggregateId, string licenseName, Guid tenant, string tenantName, Guid withdrawnBy)
    {
        return new LicenseOfferWithdrawnDomainEvent(aggregateId, licenseName, tenant, tenantName, withdrawnBy);
    }
}
