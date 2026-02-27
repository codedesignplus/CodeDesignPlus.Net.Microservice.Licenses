using System;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;

/// <summary>
/// Represents the data transfer object for a price, including billing type, billing model, and base price.
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
    /// The base price of the license.
    /// </summary>
    public decimal BasePrice { get; set; }

    /// <summary>
    /// The currency of the base price (e.g., "USD").
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// The discount percentage to apply (e.g., 15.5 for 15.5%).
    /// </summary>
    public decimal DiscountPercentage { get; set; }

    /// <summary>
    /// The tax percentage to apply (e.g., 19.0 for 19%).
    /// </summary>
    public decimal TaxPercentage { get; set; }
}
