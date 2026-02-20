using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

[EventKey<LicenseAggregate>(1, "LicenseUpdatedDomainEvent", autoCreate: false)]
public class LicenseUpdatedDomainEvent(
    Guid aggregateId,
    string name, 
    string shortDescription,
    string description, 
    List<ModuleEntity> modules, 
    List<Price> prices, 
    Icon icon,
    string TermsOfService,
    Dictionary<string, string> attributes,
    bool isActive,
    bool isPopular,
    bool showInLandingPage,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    public string Name { get; private set; } = name;
    public string ShortDescription { get; private set; } = shortDescription;
    public string Description { get; private set; } = description;
    public List<ModuleEntity> Modules { get; private set; } = modules;
    public List<Price> Prices { get; private set; } = prices;
    public Icon Icon { get; private set; } = icon;
    public string TermsOfService { get; private set; } = TermsOfService;
    public Dictionary<string, string> Attributes { get; private set; } = attributes;
    public bool IsActive { get; private set; } = isActive;
    public bool IsPopular { get; private set; } = isPopular;
    public bool ShowInLandingPage { get; private set; } = showInLandingPage;

    public static LicenseUpdatedDomainEvent Create(Guid aggregateId, string name, string shortDescription, string description, List<ModuleEntity> modules, List<Price> prices, Icon icon, string TermsOfService, Dictionary<string, string> attributes, bool isActive, bool isPopular, bool showInLandingPage)
    {
        return new LicenseUpdatedDomainEvent(aggregateId, name, shortDescription, description, modules, prices, icon, TermsOfService, attributes, isActive, isPopular, showInLandingPage);
    }
}
