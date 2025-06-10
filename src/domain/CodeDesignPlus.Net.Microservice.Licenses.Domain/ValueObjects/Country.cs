using System.Text.Json.Serialization;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Country
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public ushort Code { get; private set; }
    public string Timezone { get; private set; }
    public Currency Currency { get; private set; }

    [JsonConstructor]
    public Country(Guid id, string name, ushort code, string timezone, Currency currency)
    {
        DomainGuard.GuidIsEmpty(id, Errors.CountryIdIsEmpty);
        DomainGuard.IsNullOrEmpty(name, Errors.CountryNameIsEmpty);
        DomainGuard.IsLessThan(code, 1, Errors.CountryCodeIsInvalid);
        DomainGuard.IsNullOrEmpty(timezone, Errors.CountryTimeZoneIsEmpty);

        this.Id = id;
        this.Name = name;
        this.Code = code;
        this.Timezone = timezone;
        this.Currency = currency;
    }

    public static Country Create(Guid id, string name, ushort code, string timeZone, Currency currency)
    {
        return new Country(id, name, code, timeZone, currency);
    }
}
