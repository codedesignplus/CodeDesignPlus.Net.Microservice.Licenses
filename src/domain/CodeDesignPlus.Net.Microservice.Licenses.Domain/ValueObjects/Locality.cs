using System.Text.Json.Serialization;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Locality
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }

    [JsonConstructor]
    public Locality(Guid id, string name)
    {
        DomainGuard.IsNullOrEmpty(name, Errors.LocalityNameIsEmpty);
        DomainGuard.GuidIsEmpty(id, Errors.LocalityIdIsEmpty);

        this.Id = id;
        this.Name = name;
    }

    public static Locality Create(Guid id, string name)
    {
        return new Locality(id, name);
    }
}
