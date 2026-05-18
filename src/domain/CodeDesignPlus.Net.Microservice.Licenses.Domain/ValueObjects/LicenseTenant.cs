using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Represents a snapshot of the tenant's license constraints and validity period.
/// Implemented as a record to automatically provide value-based equality.
/// This is immutable — it captures the modules included at the moment of purchase,
/// ensuring that future changes to the license catalog do not affect existing tenants.
/// </summary>
public sealed partial record LicenseTenant
{
    [GeneratedRegex(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]{1,128}$", RegexOptions.Compiled)]
    private static partial Regex Regex();

    /// <summary>
    /// The unique identifier of the license.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The name of the license.
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// The start date of the license validity period.
    /// </summary>
    public Instant StartDate { get; init; }

    /// <summary>
    /// The end date of the license validity period.
    /// </summary>
    public Instant EndDate { get; init; }

    /// <summary>
    /// The modules included in this license at the time of purchase.
    /// Each module has a unique Id (used as ModuleId by domain microservices
    /// to decide what to provision) and a Name.
    /// </summary>
    public IReadOnlyList<LicenseModule> Modules { get; init; }

    /// <summary>
    /// Additional metadata associated with the license.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; }

    [JsonConstructor]
    private LicenseTenant(Guid id, string name, Instant startDate, Instant endDate, List<LicenseModule> modules, Dictionary<string, string> metadata)
    {
        var normalizedName = name?.Trim() ?? string.Empty;

        DomainGuard.GuidIsEmpty(id, Errors.LicenseIdIsEmpty);
        DomainGuard.IsNullOrEmpty(normalizedName, Errors.LicenseNameIsEmpty);
        DomainGuard.IsFalse(Regex().IsMatch(normalizedName), Errors.LicenseNameIsInvalid);
        DomainGuard.IsGreaterThan(startDate, endDate, Errors.LicenseStartDateGreaterThanEndDate);
        DomainGuard.IsNull(metadata, Errors.LicenseMetadataIsNull);

        this.Id = id;
        this.Name = normalizedName;
        this.StartDate = startDate;
        this.EndDate = endDate;
        this.Modules = (modules ?? []).AsReadOnly();
        this.Metadata = new ReadOnlyDictionary<string, string>(metadata);
    }

    /// <summary>
    /// Creates a new immutable snapshot of a tenant's license including its modules.
    /// </summary>
    /// <param name="id">The unique identifier of the license.</param>
    /// <param name="name">The name of the license.</param>
    /// <param name="startDate">The start date of the license validity period.</param>
    /// <param name="endDate">The end date of the license validity period.</param>
    /// <param name="modules">The modules included in this license at purchase time.</param>
    /// <param name="metadata">Additional metadata associated with the license.</param>
    /// <returns>A new immutable snapshot of a tenant's license.</returns>
    public static LicenseTenant Create(Guid id, string name, Instant startDate, Instant endDate, List<LicenseModule> modules, Dictionary<string, string> metadata)
    {
        return new LicenseTenant(id, name, startDate, endDate, modules, metadata);
    }
}

/// <summary>
/// Represents a module included in a license snapshot.
/// The Id is used as ModuleId by each domain microservice to determine
/// which templates to provision for the tenant (1 Module : N Products).
/// </summary>
public sealed record LicenseModule
{
    /// <summary>
    /// The unique identifier of the module. Used by domain micros as ModuleId
    /// to filter which templates belong to this module.
    /// </summary>
    public Guid Id { get; init; }

    /// <summary>
    /// The name of the module (e.g., "CommonAreas", "Parking", "Administration").
    /// </summary>
    public string Name { get; init; }

    /// <summary>
    /// A brief description of what this module provides.
    /// </summary>
    public string Description { get; init; }

    [JsonConstructor]
    public LicenseModule(Guid id, string name, string description)
    {
        Id = id;
        Name = name;
        Description = description;
    }
}
