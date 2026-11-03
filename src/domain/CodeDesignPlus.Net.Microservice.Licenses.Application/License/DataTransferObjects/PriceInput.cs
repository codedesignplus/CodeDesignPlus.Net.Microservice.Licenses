using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;

/// <summary>
/// WRITE contract for a license price.
/// <para>
/// <see cref="BasePrice"/> travels in MAJOR units and the rates travel as PERCENTAGES, exactly as typed
/// by the user. The handler converts both before building the <c>Price</c> value object.
/// </para>
/// <para>
/// Never use this type in a query response: read DTOs expose minor units and basis points.
/// </para>
/// </summary>
/// <param name="BillingType">The billing type for the license.</param>
/// <param name="BillingModel">The billing model for the license.</param>
/// <param name="BasePrice">The base price in major units together with its ISO 4217 currency.</param>
/// <param name="DiscountPercentage">The discount as a percentage (e.g., 15.5 for 15.5%).</param>
/// <param name="TaxPercentage">The tax as a percentage (e.g., 19 for 19%); zero unless the price is taxed.</param>
/// <param name="TaxTreatment">Whether the price is taxed, excluded or exempt (pendings/662). Taxed when omitted.</param>
/// <param name="TaxLegalBasis">The legal basis of an excluded or exempt price; empty when taxed.</param>
public sealed record PriceInput(
    BillingType BillingType,
    BillingModel BillingModel,
    MoneyInput BasePrice,
    decimal DiscountPercentage,
    decimal TaxPercentage,
    TaxTreatment TaxTreatment = TaxTreatment.Taxed,
    string? TaxLegalBasis = null)
{
    /// <summary>
    /// Builds the domain price once the currency of <see cref="BasePrice"/> is resolved.
    /// </summary>
    /// <param name="decimalDigits">The decimal digits of the currency.</param>
    /// <returns>The domain price.</returns>
    public Price ToPrice(short decimalDigits)
    {
        return Price.Create(BillingType, BasePrice.ToMoney(decimalDigits), BillingModel, BasisPoints.FromPercentage(DiscountPercentage), TaxTreatment, BasisPoints.FromPercentage(TaxPercentage), TaxLegalBasis);
    }
}
