using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Pse
{
    public string PseCode { get; private set; } = null!;
    public string TypePerson { get; private set; } = null!;

    [JsonConstructor]
    private Pse(string pseCode, string typePerson)
    {
        DomainGuard.IsNullOrEmpty(pseCode, Errors.PseCodeCannotBeNullOrEmpty);
        DomainGuard.IsGreaterThan(pseCode.Length, 34, Errors.PseCodeCannotBeGreaterThan34Characters);

        DomainGuard.IsNullOrEmpty(typePerson, Errors.TypePersonCannotBeNullOrEmpty);
        DomainGuard.IsGreaterThan(typePerson.Length, 1, Errors.TypePersonCannotBeGreaterThan2Characters);

        PseCode = pseCode;
        TypePerson = typePerson;
    }

    public static Pse Create(string pseCode, string typePerson)
    {
        return new Pse(pseCode, typePerson);
    }
}
