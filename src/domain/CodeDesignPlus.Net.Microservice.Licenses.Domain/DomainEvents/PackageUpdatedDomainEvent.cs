namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

/// <summary>
/// Se cambió un paquete complementario (pendings/739).
/// </summary>
/// <param name="aggregateId">El paquete.</param>
/// <param name="name">Su nombre.</param>
/// <param name="kind">Fijo o configurable.</param>
/// <param name="isActive">Si está activo.</param>
/// <param name="showInLandingPage">Si es público.</param>
/// <param name="eventId">El identificador del evento.</param>
/// <param name="occurredAt">Cuándo ocurrió.</param>
/// <param name="metadata">Los metadatos.</param>
[EventKey<PackageAggregate>(1, "PackageUpdatedDomainEvent")]
public class PackageUpdatedDomainEvent(
    Guid aggregateId,
    string name,
    Enums.PackageKind kind,
    bool isActive,
    bool showInLandingPage,
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
    /// Fijo o configurable.
    /// </summary>
    public Enums.PackageKind Kind { get; } = kind;

    /// <summary>
    /// Si está activo.
    /// </summary>
    public bool IsActive { get; } = isActive;

    /// <summary>
    /// Si es público.
    /// </summary>
    public bool ShowInLandingPage { get; } = showInLandingPage;

    /// <summary>
    /// Crea el evento.
    /// </summary>
    /// <returns>El evento.</returns>
    public static PackageUpdatedDomainEvent Create(Guid aggregateId, string name, Enums.PackageKind kind, bool isActive, bool showInLandingPage)
    {
        return new PackageUpdatedDomainEvent(aggregateId, name, kind, isActive, showInLandingPage);
    }
}
