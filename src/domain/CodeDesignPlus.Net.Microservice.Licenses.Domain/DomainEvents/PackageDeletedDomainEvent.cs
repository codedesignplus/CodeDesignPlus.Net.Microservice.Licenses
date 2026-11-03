namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

/// <summary>
/// Se borró un paquete complementario (pendings/739).
/// </summary>
/// <param name="aggregateId">El paquete.</param>
/// <param name="name">Su nombre.</param>
/// <param name="eventId">El identificador del evento.</param>
/// <param name="occurredAt">Cuándo ocurrió.</param>
/// <param name="metadata">Los metadatos.</param>
[EventKey<PackageAggregate>(1, "PackageDeletedDomainEvent")]
public class PackageDeletedDomainEvent(
    Guid aggregateId,
    string name,
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
    /// Crea el evento.
    /// </summary>
    /// <returns>El evento.</returns>
    public static PackageDeletedDomainEvent Create(Guid aggregateId, string name)
    {
        return new PackageDeletedDomainEvent(aggregateId, name);
    }
}
