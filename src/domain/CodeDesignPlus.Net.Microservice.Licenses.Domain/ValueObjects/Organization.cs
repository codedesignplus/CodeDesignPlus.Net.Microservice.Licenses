using CodeDesignPlus.Net.Security.Abstractions.Models;
using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Organization
{
    [GeneratedRegex(@"^(https?:\/\/)?([a-zA-Z0-9\-]+\.)+[a-zA-Z]{2,}(\/[^\s]*)?$", RegexOptions.Compiled)]
    private static partial Regex WebRegex();

    public string Name { get; private set; }
    public string? Web { get; private set; }
    public Location Location { get; private set; }

    [JsonConstructor]
    private Organization(string name, string? web, Location location)
    {
        DomainGuard.IsNullOrEmpty(name, Errors.NameIsRequired);
        DomainGuard.IsTrue(name.Length > 124, Errors.NameIsTooLong);

        if (string.IsNullOrEmpty(web))
            DomainGuard.IsFalse(WebRegex().IsMatch(name), Errors.WebContainsInvalidCharacters);

        DomainGuard.IsNull(location, Errors.LocationIsRequired);

        this.Name = name;
        this.Web = web;
        this.Location = location;
    }

    public static Organization Create(string name, string? web, Location location)
    {
        return new Organization(name, web, location);
    }
}
