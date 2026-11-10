namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

/// <summary>
/// Where an electronic document of an order stands: the invoice of what it paid or a credit note on a previous invoice
/// (pendings/661).
/// </summary>
public enum ElectronicDocumentState
{
    /// <summary>
    /// Asked of ms-electronicinvoicing and waiting for the tax authority.
    /// </summary>
    Requested = 1,

    /// <summary>
    /// Validated by the tax authority: it has its number, its CUFE and its files.
    /// </summary>
    Validated = 2,

    /// <summary>
    /// Rejected by the provider or the tax authority; the platform administrator corrects it and sends it again.
    /// </summary>
    Rejected = 3,

    /// <summary>
    /// Not sent: it needs a person first. A credit note whose invoice is not in the system (invoiced by hand or before
    /// pendings/661) stays here for the platform administrator.
    /// </summary>
    NeedsReview = 4
}
