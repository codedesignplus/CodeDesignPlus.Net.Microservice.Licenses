namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

/// <summary>
/// Se le ofreció un paquete privado a una copropiedad (pendings/739). Como con la licencia a medida (464), quien consuma el evento resuelve a quién avisar.
/// </summary>
/// <param name="aggregateId">El paquete.</param>
/// <param name="name">Su nombre.</param>
/// <param name="tenant">La copropiedad.</param>
/// <param name="tenantName">El nombre de la copropiedad.</param>
/// <param name="offeredBy">Quién lo ofreció.</param>
/// <param name="eventId">El identificador del evento.</param>
/// <param name="occurredAt">Cuándo ocurrió.</param>
/// <param name="metadata">Los metadatos.</param>
[EventKey<PackageAggregate>(1, "PackageOfferedDomainEvent")]
public class PackageOfferedDomainEvent(
    Guid aggregateId,
    string name,
    Guid tenant,
    string tenantName,
    Guid offeredBy,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    /// <summary>
    /// Su nombre.
    /// </summary>
    public string Name { get; } = name;

    /// <summary>
    /// La copropiedad.
    /// </summary>
    public Guid Tenant { get; } = tenant;

    /// <summary>
    /// El nombre de la copropiedad.
    /// </summary>
    public string TenantName { get; } = tenantName;

    /// <summary>
    /// Quién lo ofreció.
    /// </summary>
    public Guid OfferedBy { get; } = offeredBy;

    /// <summary>
    /// Crea el evento.
    /// </summary>
    /// <returns>El evento.</returns>
    public static PackageOfferedDomainEvent Create(Guid aggregateId, string name, Guid tenant, string tenantName, Guid offeredBy)
    {
        return new PackageOfferedDomainEvent(aggregateId, name, tenant, tenantName, offeredBy);
    }
}
