using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

[EventKey<LicenseAggregate>(1, "LicenseDeletedDomainEvent")]
public class LicenseDeletedDomainEvent(
    Guid aggregateId,
    string name, 
    string description, 
    List<ModuleEntity> modules, 
    List<Price> prices, 
    Dictionary<string, string> attributes,
    bool isActive,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    public string Name { get; private set; } = name;
    public string Description { get; private set; } = description;
    public List<ModuleEntity> Modules { get; private set; } = modules;
    public List<Price> Prices { get; private set; } = prices;
    public Dictionary<string, string> Attributes { get; private set; } = attributes;
    public bool IsActive { get; private set; } = isActive;

    public static LicenseDeletedDomainEvent Create(Guid aggregateId, string name, string description, List<ModuleEntity> modules, List<Price> prices, Dictionary<string, string> attributes, bool isActive)
    {
        return new LicenseDeletedDomainEvent(aggregateId, name, description, modules, prices, attributes, isActive);
    }
}
