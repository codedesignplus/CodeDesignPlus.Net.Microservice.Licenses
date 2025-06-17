using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Tenant
{
    [GeneratedRegex(@"^(https?:\/\/)?([a-zA-Z0-9\-]+\.)+[a-zA-Z]{2,}(\/[^\s]*)?$", RegexOptions.Compiled)]
    private static partial Regex WebRegex();

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Phone { get; private set; }
    public string Email { get; private set; } 
    public TypeDocument TypeDocument { get; private set; }
    public string NumberDocument { get; private set; }
    public string? Web { get; private set; }
    public Location Location { get; private set; }

    [JsonConstructor]
    private Tenant(Guid id, string name, string? web, TypeDocument typeDocument, string numberDocument, string phone, string email, Location location)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdTenantIsRequired);
        DomainGuard.IsNullOrEmpty(name, Errors.NameIsRequired);
        DomainGuard.IsGreaterThan(name.Length, 124, Errors.NameIsTooLong);
        DomainGuard.IsNullOrEmpty(numberDocument, Errors.NumberDocumentIsRequired);
        DomainGuard.IsNull(typeDocument, Errors.TypeDocumentIsRequired);
        DomainGuard.IsNullOrEmpty(phone, Errors.PhoneIsRequired);
        DomainGuard.IsNullOrEmpty(email, Errors.EmailIsRequired);

        if (!string.IsNullOrEmpty(web))
            DomainGuard.IsFalse(WebRegex().IsMatch(web), Errors.WebContainsInvalidCharacters);

        DomainGuard.IsNull(location, Errors.LocationIsRequired);

        this.Id = id;
        this.Name = name;
        this.Web = web;
        this.Location = location;
        this.TypeDocument = typeDocument;
        this.NumberDocument = numberDocument;
        this.Phone = phone;
        this.Email = email;
    }

    public static Tenant Create(Guid id, string name, string? web, TypeDocument typeDocument, string numberDocument, string phone, string email, Location location)
    {
        return new Tenant(id, name, web, typeDocument, numberDocument, phone, email, location);
    }
}
