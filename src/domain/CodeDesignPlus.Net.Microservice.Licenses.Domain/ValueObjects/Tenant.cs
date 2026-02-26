using CodeDesignPlus.Net.ValueObjects.Location;
using CodeDesignPlus.Net.ValueObjects.User;
using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Represents a snapshot of the Tenant's details for the licensing context.
/// This immutable Value Object aggregates identification, contact, and geographical data.
/// </summary>
public sealed partial record Tenant
{
    [GeneratedRegex(@"^(https?:\/\/)?([a-zA-Z0-9\-]+\.)+[a-zA-Z]{2,}(\/[^\s]*)?$", RegexOptions.Compiled)]
    private static partial Regex WebRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^\+?\d{7,15}$", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    /// <summary>
    /// The unique identifier of the tenant.
    /// </summary>
    public Guid Id { get; private set; }
    /// <summary>
    /// The name of the tenant.
    /// </summary>
    public string Name { get; private set; }
    /// <summary>
    /// The phone number of the tenant.
    /// </summary>
    public string Phone { get; private set; }
    /// <summary>
    /// The email address of the tenant.
    /// </summary>
    public string Email { get; private set; } 
    /// <summary>
    /// The type of document for the tenant.
    /// </summary>
    public TypeDocument TypeDocument { get; private set; }
    /// <summary>
    /// The document number of the tenant.
    /// </summary>
    public string NumberDocument { get; private set; }
    /// <summary>
    /// The website of the tenant.
    /// </summary>
    public string? Web { get; private set; }
    /// <summary>
    /// The location of the tenant.
    /// </summary>
    public Location Location { get; private set; }

    [JsonConstructor]
    private Tenant(Guid id, string name, string? web, TypeDocument typeDocument, string numberDocument, string phone, string email, Location location)
    {
        var normalizedName = name?.Trim() ?? string.Empty;
        var normalizedNumberDocument = numberDocument?.Trim() ?? string.Empty;
        var normalizedPhone = phone?.Trim() ?? string.Empty;
        var normalizedEmail = email?.Trim().ToLowerInvariant() ?? string.Empty;
        var normalizedWeb = web?.Trim();

        DomainGuard.GuidIsEmpty(id, Errors.IdTenantIsRequired);
        
        DomainGuard.IsNullOrEmpty(normalizedName, Errors.NameIsRequired);
        DomainGuard.IsGreaterThan(normalizedName.Length, 124, Errors.NameIsTooLong);
        
        DomainGuard.IsNull(typeDocument, Errors.TypeDocumentIsRequired);
        DomainGuard.IsNullOrEmpty(normalizedNumberDocument, Errors.NumberDocumentIsRequired);

        DomainGuard.IsNullOrEmpty(normalizedPhone, Errors.PhoneIsRequired);
        DomainGuard.IsFalse(PhoneRegex().IsMatch(normalizedPhone), Errors.PhoneContainsInvalidCharacters);

        DomainGuard.IsNullOrEmpty(normalizedEmail, Errors.EmailIsRequired);
        DomainGuard.IsFalse(EmailRegex().IsMatch(normalizedEmail), Errors.EmailContainsInvalidCharacters);

        if (!string.IsNullOrEmpty(normalizedWeb))
            DomainGuard.IsFalse(WebRegex().IsMatch(normalizedWeb), Errors.WebContainsInvalidCharacters);

        DomainGuard.IsNull(location, Errors.LocationIsRequired);

        this.Id = id;
        this.Name = normalizedName;
        this.Web = normalizedWeb;
        this.Location = location;
        this.TypeDocument = typeDocument;
        this.NumberDocument = normalizedNumberDocument;
        this.Phone = normalizedPhone;
        this.Email = normalizedEmail;
    }

    /// <summary>
    /// Creates a new immutable valid instance of the Tenant value object.
    /// </summary>
    public static Tenant Create(Guid id, string name, string? web, TypeDocument typeDocument, string numberDocument, string phone, string email, Location location)
    {
        return new Tenant(id, name, web, typeDocument, numberDocument, phone, email, location);
    }
}