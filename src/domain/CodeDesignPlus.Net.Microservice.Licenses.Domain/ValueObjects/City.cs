using System.Text.Json.Serialization;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class City
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string? TimeZone { get; private set; }

    [JsonConstructor]
    public City(Guid id, string name, string? timeZone)
    {
        DomainGuard.IsNullOrEmpty(name, Errors.CityNameIsEmpty);
        DomainGuard.GuidIsEmpty(id, Errors.CityIdIsEmpty);

        this.Id = id;
        this.Name = name;
        this.TimeZone = timeZone;
    }

    public static City Create(Guid id, string name, string? timeZone)
    {
        return new City(id, name, timeZone);
    }
}
