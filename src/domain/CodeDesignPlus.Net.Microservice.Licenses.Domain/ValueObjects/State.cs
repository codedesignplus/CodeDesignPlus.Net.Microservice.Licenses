using System.Text.Json.Serialization;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class State
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; }

    [JsonConstructor]
    public State(Guid id, string name, string code)
    {
        DomainGuard.IsNullOrEmpty(name, Errors.StateNameIsEmpty);
        DomainGuard.GuidIsEmpty(id, Errors.StateIdIsEmpty);
        DomainGuard.IsNullOrEmpty(code, Errors.StateCodeIsEmpty);

        this.Id = id;
        this.Name = name;
        this.Code = code;
    }

    public static State Create(Guid id, string name, string code)
    {
        return new State(id, name, code);
    }
}
