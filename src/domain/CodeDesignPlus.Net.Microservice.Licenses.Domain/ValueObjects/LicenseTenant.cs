using System;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Collections.ObjectModel;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Represents a snapshot of the tenant's license constraints and validity period.
/// Implemented as a record to automatically provide value-based equality.
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
    /// Additional metadata associated with the license.
    /// </summary>
    public IReadOnlyDictionary<string, string> Metadata { get; init; } 

    [JsonConstructor]
    private LicenseTenant(Guid id, string name, Instant startDate, Instant endDate, Dictionary<string, string> metadata)
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
        this.Metadata = new ReadOnlyDictionary<string, string>(metadata);
    }

    /// <summary>
    /// Creates a new immutable snapshot of a tenant's license.
    /// </summary>
    /// <param name="id">The unique identifier of the license.</param>
    /// <param name="name">The name of the license.</param>
    /// <param name="startDate">The start date of the license validity period.</param>
    /// <param name="endDate">The end date of the license validity period.</param>
    /// <param name="metadata">Additional metadata associated with the license.</param>
    /// <returns>A new immutable snapshot of a tenant's license.</returns>
    public static LicenseTenant Create(Guid id, string name, Instant startDate, Instant endDate, Dictionary<string, string> metadata)
    {
        return new LicenseTenant(id, name, startDate, endDate, metadata);
    }
}