using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;


public partial class CreditCard
{

    [GeneratedRegex(@"^\d{4}/\d{2}$")]
    private static partial Regex ExpirationDateRegex();

    public string Number { get; set; } = null!;
    public string SecurityCode { get; set; } = null!;
    public string ExpirationDate { get; set; } = null!;
    public string CardHolderName { get; set; } = null!;

    [JsonConstructor]
    private CreditCard(string number, string securityCode, string expirationDate, string cardHolderNameame)
    {
        DomainGuard.IsNullOrEmpty(number, Errors.CreditCardNumberCannotBeNullOrEmpty);
        DomainGuard.IsGreaterThan(number.Length, 20, Errors.CreditCardNumberCannotBeGreaterThan20Characters);
        DomainGuard.IsLessThan(number.Length, 13, Errors.CreditCardNumberCannotBeLessThan13Characters);

        DomainGuard.IsNullOrEmpty(securityCode, Errors.CreditCardSecurityCodeCannotBeNullOrEmpty);
        DomainGuard.IsGreaterThan(securityCode.Length, 4, Errors.CreditCardSecurityCodeCannotBeGreaterThan4Characters);
        DomainGuard.IsLessThan(securityCode.Length, 1, Errors.CreditCardSecurityCodeCannotBeLessThan3Characters);

        DomainGuard.IsNullOrEmpty(expirationDate, Errors.CreditCardExpirationDateCannotBeNullOrEmpty);
        DomainGuard.IsGreaterThan(expirationDate.Length, 7, Errors.CreditCardExpirationDateCannotBeGreaterThan7Characters);
        DomainGuard.IsFalse(ExpirationDateRegex().IsMatch(expirationDate), Errors.CreditCardExpirationDateMustBeValidFormat);

        DomainGuard.IsNullOrEmpty(cardHolderNameame, Errors.CardHolderNameCannotBeNullOrEmpty);

        Number = number;
        SecurityCode = securityCode;
        ExpirationDate = expirationDate;
        CardHolderName = cardHolderNameame;
    }
    
    public static CreditCard Create(string number, string securityCode, string expirationDate, string cardHolderName)
    {
        return new CreditCard(number, securityCode, expirationDate, cardHolderName);
    }
}
