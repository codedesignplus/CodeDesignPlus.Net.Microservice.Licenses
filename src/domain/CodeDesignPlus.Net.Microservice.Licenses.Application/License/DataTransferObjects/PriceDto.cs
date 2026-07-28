using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;

/// <summary>
/// READ contract for a license price.
/// <para>
/// Amounts are expressed in MINOR units (cents) and rates in BASIS POINTS (1900 = 19.00%).
/// The client converts both for display. Totals are computed server-side so every consumer
/// shows the same figures.
/// </para>
/// </summary>
public class PriceDto
{
    /// <summary>
    /// The billing type for the license.
    /// </summary>
    public BillingType BillingType { get; set; }
    /// <summary>
    /// The billing model for the license.
    /// </summary>
    public BillingModel BillingModel { get; set; }

    /// <summary>
    /// The base price in minor units (cents).
    /// </summary>
    public long BasePrice { get; set; }

    /// <summary>
    /// The currency of every amount in this price (e.g., "USD"), according to ISO 4217.
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// The discount rate in basis points (e.g., 1550 for 15.50%).
    /// </summary>
    public int DiscountBasisPoints { get; set; }

    /// <summary>
    /// The tax rate in basis points (e.g., 1900 for 19.00%).
    /// </summary>
    public int TaxBasisPoints { get; set; }

    /// <summary>
    /// The discount amount in minor units, derived from the base price and the discount rate.
    /// </summary>
    public long Discount { get; set; }

    /// <summary>
    /// The amount in minor units after applying the discount.
    /// </summary>
    public long SubTotal { get; set; }

    /// <summary>
    /// The tax amount in minor units, derived from the subtotal and the tax rate.
    /// </summary>
    public long Tax { get; set; }

    /// <summary>
    /// The final amount in minor units, subtotal plus tax.
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// Projects a domain <see cref="Price"/> into its read contract, carrying the amounts in minor units
    /// and resolving the totals server-side.
    /// </summary>
    /// <param name="price">The domain value object to project.</param>
    /// <returns>A new <see cref="PriceDto"/>.</returns>
    public static PriceDto FromDomain(Price price)
    {
        return new PriceDto
        {
            BillingType = price.BillingType,
            BillingModel = price.BillingModel,
            BasePrice = price.BasePrice.Amount,
            Currency = price.BasePrice.Currency,
            DiscountBasisPoints = price.DiscountBasisPoints,
            TaxBasisPoints = price.TaxBasisPoints,
            Discount = price.Discount.Amount,
            SubTotal = price.SubTotal.Amount,
            Tax = price.Tax.Amount,
            Total = price.Total.Amount
        };
    }
}
