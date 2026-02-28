using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;

public class LicenseDto : IDtoBase
{

    /// <summary>
    /// Gets the unique identifier for this license.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// Gets the display name of the license (e.g., "Pro Tier", "Enterprise").
    /// </summary>
    public string Name { get; set; } = null!;

    /// <summary>
    /// Gets the full, detailed description of what the license includes.
    /// </summary>
    public string Description { get; set; } = null!;

    /// <summary>
    /// Gets a brief summary of the license, typically used for UI cards or quick overviews.
    /// </summary>
    public string ShortDescription { get; set; } = null!;

    /// <summary>
    /// Indicates whether this license is marked as "popular" or "recommended" to highlight it in the UI.
    /// </summary>
    public bool IsPopular { get; set; } = false;

    /// <summary>
    /// Gets the list of modules (features or bounded capabilities) included in this license.
    /// </summary>
    public List<ModuleDto> Modules { get; set; } = [];

    /// <summary>
    /// Gets the list of available prices for this license. 
    /// Can contain multiple pricing models (e.g., monthly, annually) or currencies.
    /// </summary>
    public List<PriceDto> Prices { get; set; } = [];

    /// <summary>
    /// Gets a collection of custom key-value attributes associated with the license (e.g., "MaxUsers": "50").
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = [];

    /// <summary>
    /// Gets the visual icon associated with this license tier.
    /// </summary>
    public Icon Icon { get; set; } = null!;

    /// <summary>
    /// Gets the specific Terms of Service or legal agreement tied to this license.
    /// </summary>
    public string TermsOfService { get; set; } = null!;

    /// <summary>
    /// Indicates whether this license should be publicly visible on the main landing/pricing page.
    /// </summary>
    public bool ShowInLandingPage { get; set; } = false;

    /// <summary>
    /// Indicates whether this license is currently active and available for use.
    /// </summary>
    public bool IsActive { get; set; } = false;
}