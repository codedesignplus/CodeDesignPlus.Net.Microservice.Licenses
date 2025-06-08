using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Buyer
{
    [GeneratedRegex(@"^[a-zA-Z0-9_.+-]+@[a-zA-Z0-9-]+\.[a-zA-Z0-9-.]+$", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"^\+?\d{7,15}$", RegexOptions.Compiled)]
    private static partial Regex PhoneRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9áéíóúÁÉÍÓÚüÜñÑ\s\.,#\-]+$", RegexOptions.Compiled)]
    private static partial Regex AddressRegex();

    [GeneratedRegex(@"^[a-zA-Z0-9\s\-]+$", RegexOptions.Compiled)]
    private static partial Regex PostalCodeRegex();

    public string Name { get; private set; }
    public string Phone { get; private set; }
    public string Email { get; private set; }
    public string TypeDocument { get; private set; }
    public string Document { get; private set; }
    public Country Country { get; private set; }
    public State State { get; private set; }
    public City City { get; private set; }
    public string Address { get; private set; }
    public string PostalCode { get; private set; }

    [JsonConstructor]
    private Buyer(string name, string phone, string email, string typeDocument, string document, Country country, State state, City city, string address, string postalCode)
    {
        DomainGuard.IsNullOrEmpty(name, Errors.NameIsRequired);
        DomainGuard.IsTrue(name.Length > 124, Errors.NameIsTooLong);

        DomainGuard.IsNullOrEmpty(phone, Errors.PhoneIsRequired);
        DomainGuard.IsFalse(PhoneRegex().IsMatch(phone), Errors.PhoneContainsInvalidCharacters);

        DomainGuard.IsNullOrEmpty(email, Errors.EmailIsRequired);
        DomainGuard.IsFalse(EmailRegex().IsMatch(email), Errors.EmailContainsInvalidCharacters);

        DomainGuard.IsNullOrEmpty(typeDocument, Errors.TypeDocumentIsRequired);
        DomainGuard.IsFalse(typeDocument.Length > 2, Errors.TypeDocumentIsTooLong);

        DomainGuard.IsNullOrEmpty(document, Errors.DocumentIsRequired);
        DomainGuard.IsFalse(document.Length > 20, Errors.DocumentIsTooLong);

        DomainGuard.IsNull(country, Errors.CountryIsNull);
        DomainGuard.IsNull(state, Errors.StateIsNull);
        DomainGuard.IsNull(city, Errors.CityIsNull);

        DomainGuard.IsNullOrEmpty(address, Errors.AddressIsRequired);
        DomainGuard.IsFalse(address.Length > 256, Errors.AddressIsTooLong);
        DomainGuard.IsFalse(AddressRegex().IsMatch(address), Errors.AddressContainsInvalidCharacters);

        DomainGuard.IsNullOrEmpty(postalCode, Errors.PostalCodeIsRequired);
        DomainGuard.IsFalse(postalCode.Length > 16, Errors.PostalCodeIsTooLong);
        DomainGuard.IsFalse(PostalCodeRegex().IsMatch(postalCode), Errors.PostalCodeContainsInvalidCharacters);

        this.Name = name;
        this.Phone = phone;
        this.Email = email;
        this.TypeDocument = typeDocument;
        this.Document = document;
        this.Country = country;
        this.State = state;
        this.City = city;
        this.Address = address;
        this.PostalCode = postalCode;
    }

    public static Buyer Create(string name, string phone, string email, string typeDocument, string document, Country country, State state, City city, string address, string postalCode)
    {
        return new Buyer(name, phone, email, typeDocument, document, country, state, city, address, postalCode);
    }
}
