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

    /// <summary>
    /// El concepto con el que se factura la licencia (pendings/662).
    /// </summary>
    public string? InvoiceConcept { get; set; }

    /// <summary>
    /// Las copropiedades a las que se ofrece una licencia privada, con quién y cuándo (pendings/464).
    /// </summary>
    public List<LicenseOfferDto> OfferedTo { get; set; } = [];
}

/// <summary>
/// Una copropiedad a la que se ofrece una licencia privada (pendings/464).
/// </summary>
public class LicenseOfferDto
{
    /// <summary>
    /// La copropiedad.
    /// </summary>
    public Guid TenantId { get; set; }

    /// <summary>
    /// Su nombre cuando se le ofreció.
    /// </summary>
    public string TenantName { get; set; } = string.Empty;

    /// <summary>
    /// Quién se la ofreció.
    /// </summary>
    public Guid OfferedBy { get; set; }

    /// <summary>
    /// Cuándo se la ofreció.
    /// </summary>
    public Instant OfferedAt { get; set; }

    /// <summary>
    /// Whether the tenant already took the license: it has a paid and active order with it. Its offer cannot be withdrawn
    /// (error 273). Filled only in the platform view of the Super Admin (pendings/464).
    /// </summary>
    public bool Taken { get; set; }

    /// <summary>
    /// Whether the tenant has a change to this license waiting for its payment. Its offer cannot be withdrawn until the
    /// payment is approved or rejected (error 274). Filled only in the platform view of the Super Admin (pendings/464).
    /// </summary>
    public bool PaymentInProgress { get; set; }
}