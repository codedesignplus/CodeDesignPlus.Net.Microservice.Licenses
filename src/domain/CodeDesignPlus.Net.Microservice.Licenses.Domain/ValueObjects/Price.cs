using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.ValueObjects.Financial;
using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Represents the pricing configuration for a license.
/// Handles base price, discounts, taxes, and automatically calculates exact subtotals and totals.
/// </summary>
public sealed partial record Price
{
    /// <summary>
    /// The billing type for the license.
    /// </summary>
    public BillingType BillingType { get; private set; }
    /// <summary>
    /// The billing model for the license.
    /// </summary>
    public BillingModel BillingModel { get; private set; }

    /// <summary>
    /// The base price of the license. Encapsulates both the amount and the currency.
    /// </summary>
    public Money BasePrice { get; private set; } 

    /// <summary>
    /// The discount percentage to apply (e.g., 15.5 for 15.5%).
    /// </summary>
    public decimal DiscountPercentage { get; private set; }

    /// <summary>
    /// The tax percentage to apply (e.g., 19.0 for 19%).
    /// </summary>
    public decimal TaxPercentage { get; private set; }

    /// <summary>
    /// The discount amount calculated based on the base price and discount percentage.
    /// </summary>
    public Money Discount => BasePrice * (DiscountPercentage / 100m);
    
    /// <summary>
    /// The subtotal amount after applying the discount.
    /// </summary>
    public Money SubTotal => BasePrice - Discount;
    
    /// <summary>
    /// The tax amount calculated based on the subtotal and tax percentage.
    /// </summary>
    public Money Tax => SubTotal * (TaxPercentage / 100m);
    
    /// <summary>
    /// The total amount after applying the discount and adding the tax.
    /// </summary>
    public Money Total => SubTotal + Tax;


    [JsonConstructor]
    private Price(BillingType billingType, Money basePrice, BillingModel billingModel, decimal discountPercentage, decimal taxPercentage)
    {
        DomainGuard.IsNull(basePrice, Errors.PriceLicenseIsRequired);
        DomainGuard.IsLessThan(discountPercentage, 0m, Errors.DiscountLicenseCannotBeLessThanZero);
        DomainGuard.IsLessThan(taxPercentage, 0m, Errors.TaxLicenseCannotBeLessThanZero);

        if (billingModel != BillingModel.None)
            DomainGuard.IsLessThan(basePrice.Amount, 0m, Errors.PriceLicenseCannotBeLessThanZero);

        this.BillingType = billingType;
        this.BasePrice = basePrice;
        this.BillingModel = billingModel;
        this.DiscountPercentage = discountPercentage;
        this.TaxPercentage = taxPercentage;
    }
    
    /// <summary>
    /// Creates a new instance of the <see cref="Price"/> class.
    /// </summary>
    /// <param name="billingType">The billing type.</param>
    /// <param name="basePrice">The base price.</param>
    /// <param name="billingModel">The billing model.</param>
    /// <param name="discountPercentage">The discount percentage.</param>
    /// <param name="taxPercentage">The tax percentage.</param>
    /// <returns>A new instance of the <see cref="Price"/> class.</returns>
    public static Price Create(BillingType billingType, Money basePrice, BillingModel billingModel, decimal discountPercentage, decimal taxPercentage)
    {
        return new Price(billingType, basePrice, billingModel, discountPercentage, taxPercentage);
    }
}