using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class PaymentMethod
{
    public string Code { get; private set; }
    public Pse? Pse { get; private set; }
    public CreditCard? CreditCard { get; private set; }

    [JsonConstructor]
    private PaymentMethod(string code, Pse? pse, CreditCard? creditCard)
    {
        DomainGuard.IsNullOrEmpty(code, Errors.CodeOfThePaymentMethodIsRequired);

        if (pse == null)
            DomainGuard.IsNull(creditCard!, Errors.CreditCardCannotBeNull);

        if(creditCard == null)
            DomainGuard.IsNull(pse!, Errors.PseCannotBeNull);

        Code = code;
        this.Pse = pse;
        this.CreditCard = creditCard;
    }

    public static PaymentMethod Create(string code, Pse? pse, CreditCard? creditCard)
    {
        return new PaymentMethod(code, pse, creditCard);
    }
}
