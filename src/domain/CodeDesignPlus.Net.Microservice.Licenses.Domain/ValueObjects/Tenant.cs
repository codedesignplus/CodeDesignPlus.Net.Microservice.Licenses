using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Tenant
{
    [GeneratedRegex(@"^(https?:\/\/)?([a-zA-Z0-9\-]+\.)+[a-zA-Z]{2,}(\/[^\s]*)?$", RegexOptions.Compiled)]
    private static partial Regex WebRegex();

    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string? Web { get; private set; }
    public Location Location { get; private set; }

    [JsonConstructor]
    private Tenant(Guid id, string name, string? web, Location location)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdTenantIsRequired);
        DomainGuard.IsNullOrEmpty(name, Errors.NameIsRequired);
        DomainGuard.IsGreaterThan(name.Length, 124, Errors.NameIsTooLong);

        if (!string.IsNullOrEmpty(web))
            DomainGuard.IsFalse(WebRegex().IsMatch(web), Errors.WebContainsInvalidCharacters);

        DomainGuard.IsNull(location, Errors.LocationIsRequired);

        this.Name = name;
        this.Web = web;
        this.Location = location;
    }

    public static Tenant Create(Guid id, string name, string? web, Location location)
    {
        return new Tenant(id, name, web, location);
    }
}
