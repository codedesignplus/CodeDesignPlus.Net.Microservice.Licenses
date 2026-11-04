namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Options;

/// <summary>
/// To whom the platform notices of the licenses go (Kappali pendings/737, step 6).
/// </summary>
/// <remarks>
/// The role is an id of the catalog of ms-roles and changes with the product, so it lives in the <c>appsettings</c> of the
/// worker and not in the code: this service is transversal and does not know the roles of any product.
/// </remarks>
public class PlatformNoticeOptions
{
    /// <summary>
    /// The configuration section.
    /// </summary>
    public static readonly string Section = "PlatformNotices";

    /// <summary>
    /// The role of the platform administrators, who receive in their inbox the paid orders that could not be applied.
    /// </summary>
    public Guid PlatformAdministratorRole { get; set; }

    /// <summary>
    /// The operations mailbox that also receives by email, marked as important, the paid orders that could not be applied.
    /// </summary>
    /// <remarks>
    /// Empty means no email: the notice goes only to the inbox of <see cref="PlatformAdministratorRole"/>. The address is
    /// of the operator of the platform, so it lives in the configuration of each environment and not in the code.
    /// </remarks>
    public string OperationsMailbox { get; set; } = string.Empty;
}
