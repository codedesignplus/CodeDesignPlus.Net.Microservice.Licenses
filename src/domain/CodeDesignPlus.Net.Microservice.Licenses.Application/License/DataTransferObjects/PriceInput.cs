using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
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
/// <param name="TaxPercentage">The tax as a percentage (e.g., 19 for 19%).</param>
public sealed record PriceInput(
    BillingType BillingType,
    BillingModel BillingModel,
    MoneyInput BasePrice,
    decimal DiscountPercentage,
    decimal TaxPercentage);
