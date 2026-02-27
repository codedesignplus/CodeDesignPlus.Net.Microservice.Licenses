using System.Text.Json.Serialization;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Represents an immutable snapshot of a License at the exact moment of purchase.
/// It encapsulates the license details and the agreed-upon financial amounts, 
/// ensuring historical accuracy even if the main catalog changes.
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
    /// Gets the billing type (e.g., Prepaid, Postpaid).
    /// </summary>
    public BillingType BillingType { get; private set; }

    /// <summary>
    /// Gets the billing cycle model (e.g., Monthly, Yearly).
    /// </summary>
    public BillingModel BillingModel { get; private set; } 


    [JsonConstructor]
    private License(Guid id, string name, Money total, Money tax, Money subTotal, BillingType billingType, BillingModel billingModel)
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

        this.Id = id;
        this.Name = name;
        this.Total = total;
        this.Tax = tax;
        this.SubTotal = subTotal;
        this.BillingType = billingType;
        this.BillingModel = billingModel;
    }

    /// <summary>
    /// Creates a new immutable snapshot of a purchased License.
    /// </summary>
    public static License Create(Guid id, string name, Money total, Money tax, Money subTotal, BillingType billingType, BillingModel billingModel)
    {
        return new License(id, name, total, tax, subTotal, billingType, billingModel);
    }
}