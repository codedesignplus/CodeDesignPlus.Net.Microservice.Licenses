using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

/// <summary>
/// Represents the Aggregate Root for a License (e.g., a software subscription tier or product license).
/// Manages the license's presentation details, included modules, and associated pricing strategies.
/// </summary>
/// <param name="id">The unique identifier of the license.</param>
public class LicenseAggregate(Guid id) : AggregateRootBase(id)
{
    /// <summary>
    /// Gets the display name of the license (e.g., "Pro Tier", "Enterprise").
    /// </summary>
    public string Name { get; private set; } = null!;

    /// <summary>
    /// Gets the full, detailed description of what the license includes.
    /// </summary>
    public string Description { get; private set; } = null!;

    /// <summary>
    /// Gets a brief summary of the license, typically used for UI cards or quick overviews.
    /// </summary>
    public string ShortDescription { get; private set; } = null!;

    /// <summary>
    /// Indicates whether this license is marked as "popular" or "recommended" to highlight it in the UI.
    /// </summary>
    public bool IsPopular { get; private set; } = false;

    /// <summary>
    /// Gets the list of modules (features or bounded capabilities) included in this license.
    /// </summary>
    public List<ModuleEntity> Modules { get; private set; } = [];

    /// <summary>
    /// Gets the list of available prices for this license. 
    /// Can contain multiple pricing models (e.g., monthly, annually) or currencies.
    /// </summary>
    public List<Price> Prices { get; private set; } = [];

    /// <summary>
    /// Gets a collection of custom key-value attributes associated with the license (e.g., "MaxUsers": "50").
    /// </summary>
    public Dictionary<string, string> Attributes { get; private set; } = [];

    /// <summary>
    /// Gets the visual icon associated with this license tier.
    /// </summary>
    public Icon Icon { get; private set; } = null!;

    /// <summary>
    /// Gets the specific Terms of Service or legal agreement tied to this license.
    /// </summary>
    public string TermsOfService { get; private set; } = null!;

    /// <summary>
    /// Indicates whether this license should be publicly visible on the main landing/pricing page.
    /// </summary>
    public bool ShowInLandingPage { get; private set; } = false;

    /// <summary>
    /// Creates a new valid instance of the License aggregate and registers the creation domain event.
    /// </summary>
    /// <param name="id">The unique identifier to assign.</param>
    /// <param name="name">The name of the license.</param>
    /// <param name="shortDescription">A brief summary of the license.</param>
    /// <param name="description">The full description.</param>
    /// <param name="modules">The list of modules included in the license.</param>
    /// <param name="prices">The list of pricing options available.</param>
    /// <param name="icon">The visual icon for the license.</param>
    /// <param name="TermsOfService">The terms of service agreement.</param>
    /// <param name="attributes">Custom key-value metadata for the license.</param>
    /// <param name="isActive">Indicates if the license is currently active.</param>
    /// <param name="isPopular">Indicates if it should be highlighted as popular.</param>
    /// <param name="showInLandingPage">Indicates if it should be displayed on the landing page.</param>
    /// <param name="createdBy">The identifier of the user or system creating the record.</param>
    /// <returns>A new <see cref="LicenseAggregate"/> instance.</returns>
    /// <exception cref="DomainException">Thrown if any business rule validation fails.</exception>
    public static LicenseAggregate Create(Guid id, string name, string shortDescription, string description, List<ModuleEntity> modules, List<Price> prices, Icon icon, string TermsOfService, Dictionary<string, string> attributes, bool isActive, bool isPopular, bool showInLandingPage, Guid createdBy)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdLicenseIsRequired);
        DomainGuard.IsEmpty(name, Errors.NameLicenseIsRequired);
        DomainGuard.IsEmpty(shortDescription, Errors.ShortDescriptionLicenseIsRequired);
        DomainGuard.IsEmpty(description, Errors.DescriptionLicenseIsRequired);
        DomainGuard.IsNull(prices, Errors.PriceLicenseIsRequired);
        DomainGuard.GuidIsEmpty(createdBy, Errors.CreatedByLicenseIsRequired);
        DomainGuard.IsNull(icon, Errors.IconLicenseIsRequired);

        var hasDuplicatePricingStrategies = prices
            .GroupBy(p => new { p.BasePrice.Currency, p.BillingModel, p.BillingType })
            .Any(g => g.Count() > 1);

        DomainGuard.IsFalse(hasDuplicatePricingStrategies, Errors.DuplicatePricingStrategyFound);

        var aggregate = new LicenseAggregate(id)
        {
            Name = name,
            ShortDescription = shortDescription,
            Description = description,
            Modules = modules ?? [],
            Prices = prices,
            Attributes = attributes ?? [],
            Icon = icon,
            TermsOfService = TermsOfService,
            IsActive = isActive,
            IsPopular = isPopular,
            ShowInLandingPage = showInLandingPage,
            CreatedAt = SystemClock.Instance.GetCurrentInstant(),
            CreatedBy = createdBy
        };

        aggregate.AddEvent(LicenseCreatedDomainEvent.Create(id, name, shortDescription, description, modules, prices, icon, TermsOfService, attributes, isActive, isPopular, showInLandingPage));

        return aggregate;
    }

    /// <summary>
    /// Updates the license's information and registers the update domain event.
    /// </summary>
    // ... (parameters match the properties being updated)
    public void Update(string name, string shortDescription, string description, List<ModuleEntity> modules, List<Price> prices, Icon icon, string TermsOfService, Dictionary<string, string> attributes, bool isActive, bool isPopular, bool showInLandingPage, Guid updatedBy)
    {
        DomainGuard.IsEmpty(name, Errors.NameLicenseIsRequired);
        DomainGuard.IsEmpty(shortDescription, Errors.ShortDescriptionLicenseIsRequired);
        DomainGuard.IsEmpty(description, Errors.DescriptionLicenseIsRequired);
        DomainGuard.IsNull(prices, Errors.PriceLicenseIsRequired);
        DomainGuard.GuidIsEmpty(updatedBy, Errors.CreatedByLicenseIsRequired);
        DomainGuard.IsNull(icon, Errors.IconLicenseIsRequired);

        var hasDuplicatePricingStrategies = prices
            .GroupBy(p => new { p.BasePrice.Currency, p.BillingModel, p.BillingType })
            .Any(g => g.Count() > 1);

        DomainGuard.IsFalse(hasDuplicatePricingStrategies, Errors.DuplicatePricingStrategyFound);

        this.Name = name;
        this.Description = description;
        this.ShortDescription = shortDescription;
        this.Modules = modules;
        this.Prices = prices;
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

    /// <summary>
    /// Performs a logical deletion of the license and registers the deletion domain event.
    /// </summary>
    /// <param name="deletedBy">The identifier of the user or system performing the deletion.</param>
    public void Delete(Guid deletedBy)
    {
        DomainGuard.GuidIsEmpty(deletedBy, Errors.CreatedByLicenseIsRequired);

        this.IsDeleted = true;
        this.IsActive = false;
        this.DeletedAt = SystemClock.Instance.GetCurrentInstant();
        this.DeletedBy = deletedBy;

        AddEvent(LicenseDeletedDomainEvent.Create(Id, Name, Description, Modules, Prices, Attributes, IsActive));
    }

    /// <summary>
    /// Adds a new module to the license and registers the corresponding domain event.
    /// </summary>
    /// <param name="id">The unique identifier of the module.</param>
    /// <param name="name">The name of the module.</param>
    /// <param name="description">The description of the module.</param>
    /// <param name="updatedBy">The identifier of the user or system performing the action.</param>
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

    /// <summary>
    /// Removes an existing module from the license and registers the corresponding domain event.
    /// </summary>
    /// <param name="id">The unique identifier of the module to remove.</param>
    /// <param name="updatedBy">The identifier of the user or system performing the action.</param>
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