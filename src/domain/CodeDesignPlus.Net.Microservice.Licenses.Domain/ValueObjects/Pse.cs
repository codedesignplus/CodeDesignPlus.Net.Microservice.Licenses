using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Pse
{
    [GeneratedRegex(@"^https?:\/\/([a-zA-Z0-9\-\.]+)(:[0-9]+)?(\/[^\s]*)?$", RegexOptions.Compiled)]
    private static partial Regex UrlRegex();
    public string PseCode { get; private set; } = null!;
    public string TypePerson { get; private set; } = null!;
    public string PseResponseUrl { get; private set; } = null!;

    [JsonConstructor]
    private Pse(string pseCode, string typePerson, string pseResponseUrl)
    {
        DomainGuard.IsNullOrEmpty(pseCode, Errors.PseCodeCannotBeNullOrEmpty);
        DomainGuard.IsGreaterThan(pseCode.Length, 34, Errors.PseCodeCannotBeGreaterThan34Characters);

        DomainGuard.IsNullOrEmpty(typePerson, Errors.TypePersonCannotBeNullOrEmpty);
        DomainGuard.IsGreaterThan(typePerson.Length, 1, Errors.TypePersonCannotBeGreaterThan2Characters);

        DomainGuard.IsNullOrEmpty(pseResponseUrl, Errors.PseResponseUrlCannotBeNullOrEmpty);
        DomainGuard.IsGreaterThan(pseResponseUrl.Length, 200, Errors.PseResponseUrlCannotBeGreaterThan200Characters);
        DomainGuard.IsFalse(UrlRegex().IsMatch(pseResponseUrl), Errors.PseResponseUrlMustBeValidFormat);

        PseCode = pseCode;
        TypePerson = typePerson;
        PseResponseUrl = pseResponseUrl;
    }

    public static Pse Create(string pseCode, string typePerson, string pseResponseUrl)
    {
        return new Pse(pseCode, typePerson, pseResponseUrl);
    }
}
