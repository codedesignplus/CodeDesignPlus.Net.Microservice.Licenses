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
    /// The discount rate in basis points (e.g., 1550 for 15.50%).
    /// </summary>
    public int DiscountBasisPoints { get; private set; }

    /// <summary>
    /// The tax rate in basis points (e.g., 1900 for 19.00%).
    /// </summary>
    public int TaxBasisPoints { get; private set; }

    /// <summary>
    /// The discount amount calculated based on the base price and discount rate.
    /// </summary>
    public Money Discount => BasePrice * (DiscountBasisPoints / 10000m);

    /// <summary>
    /// The subtotal amount after applying the discount.
    /// </summary>
    public Money SubTotal => BasePrice - Discount;

    /// <summary>
    /// The tax amount calculated based on the subtotal and tax rate.
    /// </summary>
    public Money Tax => SubTotal * (TaxBasisPoints / 10000m);
    
    /// <summary>
    /// The total amount after applying the discount and adding the tax.
    /// </summary>
    public Money Total => SubTotal + Tax;


    [JsonConstructor]
    private Price(BillingType billingType, Money basePrice, BillingModel billingModel, int discountBasisPoints, int taxBasisPoints)
    {
        DomainGuard.IsNull(basePrice, Errors.PriceLicenseIsRequired);
        DomainGuard.IsLessThan(discountBasisPoints, 0, Errors.DiscountLicenseCannotBeLessThanZero);
        DomainGuard.IsLessThan(taxBasisPoints, 0, Errors.TaxLicenseCannotBeLessThanZero);

        if (billingModel != BillingModel.None)
            DomainGuard.IsLessThan(basePrice.Amount, 0m, Errors.PriceLicenseCannotBeLessThanZero);

        this.BillingType = billingType;
        this.BasePrice = basePrice;
        this.BillingModel = billingModel;
        this.DiscountBasisPoints = discountBasisPoints;
        this.TaxBasisPoints = taxBasisPoints;
    }
    
    /// <summary>
    /// Creates a new instance of the <see cref="Price"/> class.
    /// </summary>
    /// <param name="billingType">The billing type.</param>
    /// <param name="basePrice">The base price.</param>
    /// <param name="billingModel">The billing model.</param>
    /// <param name="discountBasisPoints">The discount rate in basis points (e.g., 1550 for 15.50%).</param>
    /// <param name="taxBasisPoints">The tax rate in basis points (e.g., 1900 for 19.00%).</param>
    /// <returns>A new instance of the <see cref="Price"/> class.</returns>
    public static Price Create(BillingType billingType, Money basePrice, BillingModel billingModel, int discountBasisPoints, int taxBasisPoints)
    {
        return new Price(billingType, basePrice, billingModel, discountBasisPoints, taxBasisPoints);
    }
}