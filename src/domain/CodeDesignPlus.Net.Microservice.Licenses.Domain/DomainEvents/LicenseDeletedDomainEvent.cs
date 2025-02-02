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
    BillingTypeEnum billingType, 
    Currency currency, 
    long price, 
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

    public BillingTypeEnum BillingType { get; private set; } = billingType;

    public Currency Currency { get; private set; } = currency;

    public long Price { get; private set; } = price;

    public Dictionary<string, string> Attributes { get; private set; } = attributes;

    public bool IsActive { get; private set; } = isActive;

    public static LicenseDeletedDomainEvent Create(Guid aggregateId, string name, string description, List<ModuleEntity> modules, BillingTypeEnum billingType, Currency currency, long price, Dictionary<string, string> attributes, bool isActive)
    {
        return new LicenseDeletedDomainEvent(aggregateId, name, description, modules, billingType, currency, price, attributes, isActive);
    }
}
