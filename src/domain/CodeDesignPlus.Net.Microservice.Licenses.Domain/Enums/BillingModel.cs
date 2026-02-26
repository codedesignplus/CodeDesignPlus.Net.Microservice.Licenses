namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

/// <summary>
/// Represents the various billing models that can be applied to a license or service.
/// </summary>
public enum BillingModel
{
    /// <summary>
    /// No billing model is applicable, or billing is handled externally/manually.
    /// This can be used for free plans or special arrangements.
    /// </summary>
    None,

    /// <summary>
    /// A single, fixed fee for the service or plan, regardless of usage, number of users, or units.
    /// Example: Software with a fixed monthly/annual price for access to all its features.
    /// </summary>
    FlatRate,

    /// <summary>
    /// Pricing based on the total number of registered or licensed users.
    /// Example: CRMs, collaboration tools, project management software.
    /// </summary>
    PerUser,

    /// <summary>
    /// Pricing based on the number of users who actively use the system within a given period.
    /// Example: Systems where not all registered users are consistently active.
    /// </summary>
    PerActiveUser,

    /// <summary>
    /// Pricing based on a primary "unit" managed by the system. The definition of a "unit" is application-specific.
    /// Examples:
    /// - Property Management: Per apartment/house.
    /// - Inventory System: Per number of SKUs or managed products.
    /// - Project Management System: Per active project.
    /// - E-learning Platform: Per published course or enrolled student.
    /// - Booking System: Per reservable resource (room, table, car).
    /// </summary>
    PerUnit,

    /// <summary>
    /// Pricing based on a grouping of "units" or a higher-level organizational entity.
    /// Examples:
    /// - Property Management: Per tower/block.
    /// - Multi-Branch Companies: Per branch/department.
    /// - Educational Platform: Per institution/school.
    /// </summary>
    PerGroup, // Or PerOrganizationalUnit, PerSite

    /// <summary>
    /// Pricing based on the consumption of specific resources or transaction volume.
    /// Examples:
    /// - Cloud Services: Per GB of storage, compute hours, data transfers.
    /// - API Services: Per number of API calls.
    /// - Email Marketing Platform: Per number of emails sent or contacts stored.
    /// - Payment Gateways: Per processed transaction.
    /// </summary>
    UsageBased, // Or ConsumptionBased

    /// <summary>
    /// Pricing is defined by tiers or "bands" of usage or quantity (of users, units, etc.).
    /// The price within the *same* plan varies by these tiers.
    /// Examples:
    /// - 1-10 users = $X, 11-50 users = $Y.
    /// - Up to 1000 API calls/month = $A, 1001-10000 API calls/month = $B.
    /// - Management of up to 50 units = $C, 51-200 units = $D.
    /// </summary>
    TieredUsage, // Or VolumeBasedTiered

    /// <summary>
    /// Pricing based on specific features or modules that the customer chooses to activate or purchase.
    /// Example: Base software with paid add-on modules (advanced accounting, premium integrations).
    /// </summary>
    PerFeature, // Or PerModule

    /// <summary>
    /// A model that combines several of the above. Specific details would be defined elsewhere (e.g., in attributes).
    /// Example: A base flat rate + a cost per additional user over a limit.
    /// </summary>
    Hybrid,

    /// <summary>
    /// A completely custom or negotiated billing model, not covered by standard options.
    /// Requires manual configuration or logic.
    /// </summary>
    Custom
}