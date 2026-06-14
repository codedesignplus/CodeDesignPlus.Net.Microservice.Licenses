using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

/// <summary>
/// Flattened DTO for the License snapshot within a Subscription response.
/// Money amounts are converted from minor units to decimal for REST consumers.
/// </summary>
public class SubscriptionLicenseDto
{
    /// <summary>
    /// The original unique identifier of the license in the catalog.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// The name of the license at the time of purchase.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// The final total cost including taxes, expressed in the main currency unit (e.g., 150000.00 for COP).
    /// </summary>
    public decimal Total { get; set; }

    /// <summary>
    /// The applied tax amount, expressed in the main currency unit.
    /// </summary>
    public decimal Tax { get; set; }

    /// <summary>
    /// The base cost before taxes, expressed in the main currency unit.
    /// </summary>
    public decimal SubTotal { get; set; }

    /// <summary>
    /// The ISO 4217 currency code (e.g., "COP", "USD").
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// The billing type (e.g., Monthly, Yearly).
    /// </summary>
    public BillingType BillingType { get; set; }

    /// <summary>
    /// The billing cycle model (e.g., FlatRate, PerUser, PerActiveUser).
    /// </summary>
    public BillingModel BillingModel { get; set; }

    /// <summary>
    /// Full description of what the license includes.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Brief summary for UI cards or quick overviews.
    /// </summary>
    public string ShortDescription { get; set; } = string.Empty;

    /// <summary>
    /// The visual icon associated with this license tier.
    /// </summary>
    public Icon Icon { get; set; } = null!;

    /// <summary>
    /// Terms of Service tied to this license.
    /// </summary>
    public string TermsOfService { get; set; } = string.Empty;

    /// <summary>
    /// Whether this license is marked as "popular" or "recommended".
    /// </summary>
    public bool IsPopular { get; set; }

    /// <summary>
    /// Whether this license should be visible on the landing/pricing page.
    /// </summary>
    public bool ShowInLandingPage { get; set; }

    /// <summary>
    /// Custom key-value attributes (e.g., "MaxUsers": "50").
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = [];

    /// <summary>
    /// Modules included in this license at purchase time.
    /// </summary>
    public List<LicenseModule> Modules { get; set; } = [];
}
