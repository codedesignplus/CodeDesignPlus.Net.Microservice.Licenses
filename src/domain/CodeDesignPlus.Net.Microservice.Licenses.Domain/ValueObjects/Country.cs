using System.Text.Json.Serialization;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Country
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Alpha2 { get; private set; }
    public ushort Code { get; private set; }
    public string Timezone { get; private set; }
    public Currency Currency { get; private set; }

    [JsonConstructor]
    public Country(Guid id, string name, string alpha2, ushort code, string timezone, Currency currency)
    {
        DomainGuard.GuidIsEmpty(id, Errors.CountryIdIsEmpty);
        DomainGuard.IsNullOrEmpty(name, Errors.CountryNameIsEmpty);
        DomainGuard.IsNullOrEmpty(alpha2, Errors.CountryAlpha2IsEmpty);
        DomainGuard.IsLessThan(code, 1, Errors.CountryCodeIsInvalid);
        DomainGuard.IsNullOrEmpty(timezone, Errors.CountryTimeZoneIsEmpty);

        this.Id = id;
        this.Name = name;
        this.Alpha2 = alpha2;
        this.Code = code;
        this.Timezone = timezone;
        this.Currency = currency;
    }

    public static Country Create(Guid id, string name, string alpha2, ushort code, string timezone, Currency currency)
    {
        return new Country(id, name, alpha2, code, timezone, currency);
    }
}
