using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

public class LicenseAggregate(Guid id) : AggregateRootBase(id)
{
    public string Name { get; private set; } = null!;
    public string Description { get; private set; } = null!;
    public List<ModuleEntity> Modules { get; private set; } = [];
    public List<Price> Prices { get; private set; } = [];
    public Dictionary<string, string> Attributes { get; private set; } = [];
    public Guid IdLogo { get; private set; } = Guid.Empty;
    public string TermsOfService { get; private set; } = null!;

    private LicenseAggregate(Guid id, string name, string description, List<ModuleEntity> modules, List<Price> price, Guid idLogo, string termOfService, Dictionary<string, string> attributes, bool isActive, Guid createdBy) : this(id)
    {
        this.Name = name;
        this.Description = description;
        this.Modules = modules ?? [];
        this.Prices = price;
        this.Attributes = attributes ?? [];
        this.IdLogo = idLogo;
        this.TermsOfService = termOfService;
        this.IsActive = isActive;

        this.CreatedAt = SystemClock.Instance.GetCurrentInstant();
        this.CreatedBy = createdBy;

        AddEvent(LicenseCreatedDomainEvent.Create(Id, Name, Description, Modules, Prices, IdLogo, TermsOfService, Attributes, IsActive));
    }

    public static LicenseAggregate Create(Guid id, string name, string description, List<ModuleEntity> modules, List<Price> price, Guid idLogo, string termOfService, Dictionary<string, string> attributes, bool isActive, Guid createdBy)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdLicenseIsRequired);
        DomainGuard.IsEmpty(name, Errors.NameLicenseIsRequired);
        DomainGuard.IsEmpty(description, Errors.DescriptionLicenseIsRequired);
        DomainGuard.IsNull(price, Errors.PriceLicenseIsRequired);
        DomainGuard.GuidIsEmpty(createdBy, Errors.CreatedByLicenseIsRequired);

        return new LicenseAggregate(id, name, description, modules, price, idLogo, termOfService, attributes, isActive, createdBy);
    }

    public void Update(string name, string description, List<ModuleEntity> modules, List<Price> price, Guid idLogo, string termOfService, Dictionary<string, string> attributes, bool isActive, Guid updatedBy)
    {
        DomainGuard.IsEmpty(name, Errors.NameLicenseIsRequired);
        DomainGuard.IsEmpty(description, Errors.DescriptionLicenseIsRequired);
        DomainGuard.IsNull(price, Errors.PriceLicenseIsRequired);
        DomainGuard.GuidIsEmpty(updatedBy, Errors.CreatedByLicenseIsRequired);

        this.Name = name;
        this.Description = description;
        this.Modules = modules;
        this.Prices = price;
        this.Attributes = attributes;
        this.IdLogo = idLogo;
        this.TermsOfService = termOfService;
        this.IsActive = isActive;

        this.UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        this.UpdatedBy = updatedBy;

        AddEvent(LicenseUpdatedDomainEvent.Create(Id, Name, Description, Modules, Prices, IdLogo, TermsOfService, Attributes, IsActive));
    }

    public void Delete(Guid deletedBy)
    {
        DomainGuard.GuidIsEmpty(deletedBy, Errors.CreatedByLicenseIsRequired);

        this.UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        this.UpdatedBy = deletedBy;

        AddEvent(LicenseDeletedDomainEvent.Create(Id, Name, Description, Modules, Prices, Attributes, IsActive));
    }

    public void AddModule(Guid id, string name, Guid updatedBy)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdModuleIsRequired);
        DomainGuard.IsEmpty(name, Errors.NameLicenseIsRequired);
        DomainGuard.GuidIsEmpty(updatedBy, Errors.CreatedByLicenseIsRequired);
        DomainGuard.IsTrue(this.Modules.Any(x => x.Id == id), Errors.ModuleAlreadyExists);

        var module = new ModuleEntity()
        {
            Id = id,
            Name = name
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
