using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

public class LicenseAggregate(Guid id) : AggregateRootBase(id)
{
    public string Name { get; private set; } = null!;

    public string Description { get; private set; } = null!;

    public List<ModuleEntity> Modules { get; private set; } = [];

    public BillingTypeEnum BillingType { get; private set; }

    public Currency Currency { get; private set; } = null!;

    public long Price { get; private set; }

    public Dictionary<string, string> Attributes { get; private set; } = [];

    private LicenseAggregate(Guid id, string name, string description, List<ModuleEntity> modules, BillingTypeEnum billingType, Currency currency, long price, Dictionary<string, string> attributes, Guid createdBy) : this(id)
    {
        this.Name = name;
        this.Description = description;
        this.Modules = modules ?? [];
        this.BillingType = billingType;
        this.Currency = currency;
        this.Price = price;
        this.Attributes = attributes ?? [];

        this.CreatedAt = SystemClock.Instance.GetCurrentInstant();
        this.CreatedBy = createdBy;

        AddEvent(LicenseCreatedDomainEvent.Create(Id, Name, Description, Modules, BillingType, Currency, Price, Attributes, IsActive));
    }

    public static LicenseAggregate Create(Guid id, string name, string description, List<ModuleEntity> modules, BillingTypeEnum billingType, Currency currency, long price, Dictionary<string, string> attributes, Guid createdBy)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdLicenseIsRequired);
        DomainGuard.IsEmpty(name, Errors.NameLicenseIsRequired);
        DomainGuard.IsEmpty(description, Errors.DescriptionLicenseIsRequired);
        DomainGuard.IsNull(currency, Errors.CurrencyLicenseIsRequired);
        DomainGuard.IsLessThan(price, 0, Errors.PriceLicenseCannotBeLessThanZero);
        DomainGuard.GuidIsEmpty(createdBy, Errors.CreatedByLicenseIsRequired);

        return new LicenseAggregate(id, name, description, modules, billingType, currency, price, attributes, createdBy);
    }

    public void Update(string name, string description, List<ModuleEntity> modules, BillingTypeEnum billingType, Currency currency, long price, Dictionary<string, string> attributes, Guid updatedBy)
    {
        DomainGuard.IsEmpty(name, Errors.NameLicenseIsRequired);
        DomainGuard.IsEmpty(description, Errors.DescriptionLicenseIsRequired);
        DomainGuard.IsNull(currency, Errors.CurrencyLicenseIsRequired);
        DomainGuard.IsLessThan(price, 0, Errors.PriceLicenseCannotBeLessThanZero);
        DomainGuard.GuidIsEmpty(updatedBy, Errors.CreatedByLicenseIsRequired);

        this.Name = name;
        this.Description = description;
        this.Modules = modules;
        this.BillingType = billingType;
        this.Currency = currency;
        this.Price = price;
        this.Attributes = attributes;

        this.UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        this.UpdatedBy = updatedBy;

        AddEvent(LicenseUpdatedDomainEvent.Create(Id, Name, Description, Modules, BillingType, Currency, Price, Attributes, IsActive));
    }

    public void Delete(Guid deletedBy)
    {
        DomainGuard.GuidIsEmpty(deletedBy, Errors.CreatedByLicenseIsRequired);

        this.UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        this.UpdatedBy = deletedBy;

        AddEvent(LicenseDeletedDomainEvent.Create(Id, Name, Description, Modules, BillingType, Currency, Price, Attributes, IsActive));
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

        DomainGuard.IsNull(module, Errors.ModuleNotFound);

        this.Modules.Remove(module);

        this.UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        this.UpdatedBy = updatedBy;

        AddEvent(LicenseModuleRemovedDomainEvent.Create(Id, module.Id, module.Name));
    }    
}
