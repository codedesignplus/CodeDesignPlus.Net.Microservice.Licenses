namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

/// <summary>
/// Se le ofreció una licencia privada a una copropiedad: ya la puede elegir al cambiar de licencia (pendings/464).
/// </summary>
/// <remarks>
/// ms-licenses no sabe a quién avisar (es de CodeDesignPlus y no conoce los cargos de Kappali): quien consuma el evento
/// resuelve los destinatarios, como con el aviso de vencimiento (pendings/736).
/// </remarks>
/// <param name="aggregateId">La licencia.</param>
/// <param name="licenseName">El nombre de la licencia.</param>
/// <param name="tenant">La copropiedad a la que se ofrece.</param>
/// <param name="tenantName">El nombre de la copropiedad.</param>
/// <param name="offeredBy">Quién la ofreció.</param>
/// <param name="eventId">El identificador del evento.</param>
/// <param name="occurredAt">Cuándo ocurrió.</param>
/// <param name="metadata">Los metadatos.</param>
[EventKey<LicenseAggregate>(1, "LicenseOfferedDomainEvent")]
public class LicenseOfferedDomainEvent(
    Guid aggregateId,
    string licenseName,
    Guid tenant,
    string tenantName,
    Guid offeredBy,
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
    /// La copropiedad a la que se ofrece.
    /// </summary>
    public Guid Tenant { get; } = tenant;

    /// <summary>
    /// El nombre de la copropiedad.
    /// </summary>
    public string TenantName { get; } = tenantName;

    /// <summary>
    /// Quién la ofreció.
    /// </summary>
    public Guid OfferedBy { get; } = offeredBy;

    /// <summary>
    /// Crea el evento.
    /// </summary>
    /// <param name="aggregateId">La licencia.</param>
    /// <param name="licenseName">El nombre de la licencia.</param>
    /// <param name="tenant">La copropiedad.</param>
    /// <param name="tenantName">El nombre de la copropiedad.</param>
    /// <param name="offeredBy">Quién la ofreció.</param>
    /// <returns>El evento.</returns>
    public static LicenseOfferedDomainEvent Create(Guid aggregateId, string licenseName, Guid tenant, string tenantName, Guid offeredBy)
    {
        return new LicenseOfferedDomainEvent(aggregateId, licenseName, tenant, tenantName, offeredBy);
    }
}
