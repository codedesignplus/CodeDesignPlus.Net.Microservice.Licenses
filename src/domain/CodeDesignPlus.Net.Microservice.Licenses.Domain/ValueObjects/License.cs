using System.Text.Json.Serialization;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Represents an immutable snapshot of a License at the exact moment of purchase.
/// It encapsulates the complete license details, included modules, metadata attributes,
/// and the agreed-upon financial amounts, ensuring historical accuracy even if the main catalog changes.
/// </summary>
public sealed record License
{
    /// <summary>
    /// Gets the original unique identifier of the license in the catalog.
    /// </summary>
    public Guid Id { get; private set; }

    /// <summary>
    /// Gets the name of the license at the time of purchase.
    /// </summary>
    public string Name { get; private set; }

    /// <summary>
    /// Gets the final total cost of the license, including taxes.
    /// </summary>
    public Money Total { get; private set; }

    /// <summary>
    /// Gets the applied tax amount.
    /// </summary>
    public Money Tax { get; private set; }

    /// <summary>
    /// Gets the base cost of the license before taxes.
    /// </summary>
    public Money SubTotal { get; private set; }

    /// <summary>
    /// Gets the billing type (e.g., Monthly, Yearly).
    /// </summary>
    public BillingType BillingType { get; private set; }

    /// <summary>
    /// Gets the billing cycle model (e.g., FlatRate, PerUser, PerActiveUser).
    /// </summary>
    public BillingModel BillingModel { get; private set; }

    /// <summary>
    /// Gets the full, detailed description of what the license includes.
    /// </summary>
    public string Description { get; private set; }

    /// <summary>
    /// Gets a brief summary of the license, typically used for UI cards or quick overviews.
    /// </summary>
    public string ShortDescription { get; private set; }

    /// <summary>
    /// Gets the visual icon associated with this license tier.
    /// </summary>
    public Icon Icon { get; private set; }

    /// <summary>
    /// Gets the specific Terms of Service or legal agreement tied to this license.
    /// </summary>
    public string TermsOfService { get; private set; }

    /// <summary>
    /// Indicates whether this license is marked as "popular" or "recommended" to highlight it in the UI.
    /// </summary>
    public bool IsPopular { get; private set; }

    /// <summary>
    /// Indicates whether this license should be publicly visible on the main landing/pricing page.
    /// </summary>
    public bool ShowInLandingPage { get; private set; }

    /// <summary>
    /// Gets a collection of custom key-value attributes associated with the license (e.g., "MaxUsers": "50").
    /// </summary>
    public Dictionary<string, string> Attributes { get; private set; }

    /// <summary>
    /// Gets the list of modules (features or bounded capabilities) included in this license at purchase time.
    /// </summary>
    public List<LicenseModule> Modules { get; private set; } 


    [JsonConstructor]
    private License(
        Guid id,
        string name,
        Money total,
        Money tax,
        Money subTotal,
        BillingType billingType,
        BillingModel billingModel,
        string description,
        string shortDescription,
        Icon icon,
        string termsOfService,
        bool isPopular,
        bool showInLandingPage,
        Dictionary<string, string> attributes,
        List<LicenseModule> modules)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdOfLicenseIsRequired);
        DomainGuard.IsNullOrEmpty(name, Errors.NameOfLicenseIsRequired);

        DomainGuard.IsNull(total, Errors.TotalOfLicenseIsRequired);
        DomainGuard.IsNull(tax, Errors.TaxOfLicenseIsRequired);
        DomainGuard.IsNull(subTotal, Errors.SubTotalOfLicenseIsRequired);

        bool currenciesMatch = total.Currency == tax.Currency && tax.Currency == subTotal.Currency;
        DomainGuard.IsFalse(currenciesMatch, Errors.CurrenciesMustMatch);

        DomainGuard.IsLessThan(total.Amount, 0m, Errors.TotalOfLicenseShouldBeGreaterThanZero);
        DomainGuard.IsLessThan(tax.Amount, 0m, Errors.TaxOfLicenseShouldBeGreaterThanZero);
        DomainGuard.IsLessThan(subTotal.Amount, 0m, Errors.SubTotalOfLicenseShouldBeGreaterThanZero);

        bool isMathCorrect = total == (subTotal + tax);
        DomainGuard.IsFalse(isMathCorrect, Errors.TotalIsNotEqualToTaxAndSubTotal);

        DomainGuard.IsNullOrEmpty(description, Errors.DescriptionOfLicenseIsRequired);
        DomainGuard.IsNullOrEmpty(shortDescription, Errors.ShortDescriptionOfLicenseIsRequired);
        DomainGuard.IsNull(icon, Errors.IconLicenseIsRequired);
        DomainGuard.IsNullOrEmpty(termsOfService, Errors.TermsOfServiceOfLicenseIsRequired);
        DomainGuard.IsNull(attributes, Errors.AttributesLicenseIsRequired);
        DomainGuard.IsNull(modules, Errors.ModulesLicenseIsRequired);

        this.Id = id;
        this.Name = name;
        this.Total = total;
        this.Tax = tax;
        this.SubTotal = subTotal;
        this.BillingType = billingType;
        this.BillingModel = billingModel;
        this.Description = description;
        this.ShortDescription = shortDescription;
        this.Icon = icon;
        this.TermsOfService = termsOfService;
        this.IsPopular = isPopular;
        this.ShowInLandingPage = showInLandingPage;
        this.Attributes = attributes;
        this.Modules = modules;
    }

    /// <summary>
    /// Creates a new immutable snapshot of a purchased License with complete metadata and modules.
    /// </summary>
    public static License Create(
        Guid id,
        string name,
        Money total,
        Money tax,
        Money subTotal,
        BillingType billingType,
        BillingModel billingModel,
        string description,
        string shortDescription,
        Icon icon,
        string termsOfService,
        bool isPopular,
        bool showInLandingPage,
        Dictionary<string, string> attributes,
        List<LicenseModule> modules)
    {
        return new License(
            id,
            name,
            total,
            tax,
            subTotal,
            billingType,
            billingModel,
            description,
            shortDescription,
            icon,
            termsOfService,
            isPopular,
            showInLandingPage,
            attributes,
            modules);
    }
}