using System.Text.Json.Serialization;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Neighborhood
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }

    [JsonConstructor]
    public Neighborhood(Guid id, string name)
    {
        DomainGuard.IsNullOrEmpty(name, Errors.NeighborhoodNameIsEmpty);
        DomainGuard.GuidIsEmpty(id, Errors.NeighborhoodIdIsEmpty);

        this.Id = id;
        this.Name = name;
    }

    public static Neighborhood Create(Guid id, string name)
    {
        return new Neighborhood(id, name);
    }
}
