namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

[EventKey<LicenseAggregate>(1, "LicenseModuleAddedDomainEvent")]
public class LicenseModuleAddedDomainEvent(
    Guid aggregateId,
    Guid idModule,
    string name,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{    
    public Guid IdModule { get; private set; } = idModule;
    public string Name { get; private set; } = name;
    
    public static LicenseModuleAddedDomainEvent Create(Guid aggregateId, Guid idModule, string name)
    {
        return new LicenseModuleAddedDomainEvent(aggregateId, idModule, name);
    }
}
