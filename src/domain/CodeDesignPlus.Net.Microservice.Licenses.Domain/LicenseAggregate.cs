using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

public class LicenseAggregate(Guid id) : AggregateRootBase(id)
{
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public string ShortDescription { get; private set; } = null!;
    public bool IsPopular { get; private set; } = false;
    public List<ModuleEntity> Modules { get; private set; } = [];
    public List<Price> Prices { get; private set; } = [];
    public Dictionary<string, string> Attributes { get; private set; } = [];
    public Icon Icon { get; private set; } = null!;
    public string TermsOfService { get; private set; } = null!;
    public bool ShowInLandingPage { get; private set; } = false;

    private LicenseAggregate(Guid id, string name, string shortDescription, string description, List<ModuleEntity> modules, List<Price> price, Icon icon, string TermsOfService, Dictionary<string, string> attributes, bool isActive, bool isPopular, bool showInLandingPage, Guid createdBy) : this(id)
    {
        this.Name = name;
        this.ShortDescription = shortDescription;
        this.Description = description;
        this.Modules = modules ?? [];
        this.Prices = price;
        this.Attributes = attributes ?? [];
        this.Icon = icon;
        this.TermsOfService = TermsOfService;
        this.IsActive = isActive;
        this.IsPopular = isPopular;
        this.ShowInLandingPage = showInLandingPage;

        this.CreatedAt = SystemClock.Instance.GetCurrentInstant();
        this.CreatedBy = createdBy;

        AddEvent(LicenseCreatedDomainEvent.Create(Id, Name, ShortDescription, Description, Modules, Prices, Icon, TermsOfService, Attributes, IsActive, IsPopular, ShowInLandingPage));
    }

    public static LicenseAggregate Create(Guid id, string name, string shortDescription, string description, List<ModuleEntity> modules, List<Price> price, Icon icon, string TermsOfService, Dictionary<string, string> attributes, bool isActive, bool isPopular, bool showInLandingPage, Guid createdBy)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdLicenseIsRequired);
        DomainGuard.IsEmpty(name, Errors.NameLicenseIsRequired);
        DomainGuard.IsEmpty(shortDescription, Errors.ShortDescriptionLicenseIsRequired);
        DomainGuard.IsEmpty(description, Errors.DescriptionLicenseIsRequired);
        DomainGuard.IsNull(price, Errors.PriceLicenseIsRequired);
        DomainGuard.GuidIsEmpty(createdBy, Errors.CreatedByLicenseIsRequired);
        DomainGuard.IsNull(icon, Errors.IconLicenseIsRequired);

        return new LicenseAggregate(id, name, shortDescription, description, modules, price, icon, TermsOfService, attributes, isActive, isPopular, showInLandingPage, createdBy);
    }

    public void Update(string name, string shortDescription, string description, List<ModuleEntity> modules, List<Price> price, Icon icon, string TermsOfService, Dictionary<string, string> attributes, bool isActive, bool isPopular, bool showInLandingPage, Guid updatedBy)
    {
        DomainGuard.IsEmpty(name, Errors.NameLicenseIsRequired);
        DomainGuard.IsEmpty(shortDescription, Errors.ShortDescriptionLicenseIsRequired);
        DomainGuard.IsEmpty(description, Errors.DescriptionLicenseIsRequired);
        DomainGuard.IsNull(price, Errors.PriceLicenseIsRequired);
        DomainGuard.GuidIsEmpty(updatedBy, Errors.CreatedByLicenseIsRequired);
        DomainGuard.IsNull(icon, Errors.IconLicenseIsRequired);

        this.Name = name;
        this.Description = description;
        this.ShortDescription = shortDescription;
        this.Modules = modules;
        this.Prices = price;
        this.Attributes = attributes;
        this.Icon = icon;
        this.TermsOfService = TermsOfService;
        this.IsActive = isActive;
        this.IsPopular = isPopular;
        this.ShowInLandingPage = showInLandingPage;

        this.UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        this.UpdatedBy = updatedBy;

        AddEvent(LicenseUpdatedDomainEvent.Create(Id, Name, ShortDescription, Description, Modules, Prices, Icon, TermsOfService, Attributes, IsActive, IsPopular, ShowInLandingPage));
    }

    public void Delete(Guid deletedBy)
    {
        DomainGuard.GuidIsEmpty(deletedBy, Errors.CreatedByLicenseIsRequired);

        this.UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        this.UpdatedBy = deletedBy;

        AddEvent(LicenseDeletedDomainEvent.Create(Id, Name, Description, Modules, Prices, Attributes, IsActive));
    }

    public void AddModule(Guid id, string name, string description, Guid updatedBy)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdModuleIsRequired);
        DomainGuard.IsEmpty(name, Errors.NameLicenseIsRequired);
        DomainGuard.IsEmpty(description, Errors.DescriptionModuleIsRequired);
        DomainGuard.GuidIsEmpty(updatedBy, Errors.CreatedByLicenseIsRequired);
        DomainGuard.IsTrue(this.Modules.Any(x => x.Id == id), Errors.ModuleAlreadyExists);

        var module = new ModuleEntity()
        {
            Id = id,
            Name = name,
            Description = description
        };

        this.Modules.Add(module);

        this.UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        this.UpdatedBy = updatedBy;

        AddEvent(LicenseModuleAddedDomainEvent.Create(Id, module.Id, module.Name));
    }

    public void RemoveModule(Guid id, Guid updatedBy)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdModuleIsRequired);
        DomainGuard.GuidIsEmpty(updatedBy, Errors.CreatedByLicenseIsRequired);

        var module = this.Modules.FirstOrDefault(x => x.Id == id);

        DomainGuard.IsNull(module!, Errors.ModuleNotFound);

        this.Modules.Remove(module);

        this.UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        this.UpdatedBy = updatedBy;

        AddEvent(LicenseModuleRemovedDomainEvent.Create(Id, module.Id, module.Name));
    }
}
