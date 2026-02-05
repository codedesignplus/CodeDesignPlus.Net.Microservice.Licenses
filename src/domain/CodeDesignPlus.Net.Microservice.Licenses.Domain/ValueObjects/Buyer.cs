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

    public Guid BuyerId { get; private set; }
    public string Name { get; private set; }
    public string Phone { get; private set; }
    public string Email { get; private set; }
    public TypeDocument TypeDocument { get; private set; }
    public string Document { get; private set; }

    [JsonConstructor]
    private Buyer(string name, string phone, string email, TypeDocument typeDocument, string document)
    {
        DomainGuard.IsNullOrEmpty(name, Errors.NameIsRequired);
        DomainGuard.IsGreaterThan(name.Length, 124, Errors.NameIsTooLong);

        DomainGuard.IsNullOrEmpty(phone, Errors.PhoneIsRequired);
        DomainGuard.IsFalse(PhoneRegex().IsMatch(phone), Errors.PhoneContainsInvalidCharacters);

        DomainGuard.IsNullOrEmpty(email, Errors.EmailIsRequired);
        DomainGuard.IsFalse(EmailRegex().IsMatch(email), Errors.EmailContainsInvalidCharacters);

        DomainGuard.IsNull(typeDocument, Errors.TypeDocumentIsRequired);

        DomainGuard.IsNullOrEmpty(document, Errors.DocumentIsRequired);
        DomainGuard.IsGreaterThan(document.Length, 20, Errors.DocumentIsTooLong);

        this.Name = name;
        this.Phone = phone;
        this.Email = email;
        this.TypeDocument = typeDocument;
        this.Document = document;
    }

    public static Buyer Create(string name, string phone, string email, TypeDocument typeDocument, string document)
    {
        return new Buyer(name, phone, email, typeDocument, document);
    }

    public void SetBuyerId(Guid buyerId)
    {
        DomainGuard.GuidIsEmpty(buyerId, Errors.IdBuyerIsRequired);

        this.BuyerId = buyerId;
    }
}
