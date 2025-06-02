using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

[EventKey<LicenseAggregate>(1, "LicenseUpdatedDomainEvent")]
public class LicenseUpdatedDomainEvent(
     Guid aggregateId,
    string name, 
    string description, 
    List<ModuleEntity> modules, 
    List<Price> prices, 
    Guid idLogo,
    string TermsOfService,
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
    public Guid IdLogo { get; private set; } = idLogo;
    public string TermsOfService { get; private set; } = TermsOfService;
    public Dictionary<string, string> Attributes { get; private set; } = attributes;
    public bool IsActive { get; private set; } = isActive;

    public static LicenseUpdatedDomainEvent Create(Guid aggregateId, string name, string description, List<ModuleEntity> modules, List<Price> prices, Guid idLogo, string TermsOfService, Dictionary<string, string> attributes, bool isActive)
    {
        return new LicenseUpdatedDomainEvent(aggregateId, name, description, modules, prices, idLogo, TermsOfService, attributes, isActive);
    }
}
