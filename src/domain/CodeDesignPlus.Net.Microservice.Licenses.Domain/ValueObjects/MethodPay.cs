using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class PaymentMethod
{
    public Pse? Pse { get; private set; }
    public CreditCard? CreditCard { get; private set; }

    [JsonConstructor]
    private PaymentMethod(Pse? pse, CreditCard? creditCard)
    {
        if(pse == null)
            DomainGuard.IsNull(creditCard!, Errors.CreditCardCannotBeNull);

        if(creditCard == null)
            DomainGuard.IsNull(pse!, Errors.PseCannotBeNull);

        this.Pse = pse;
        this.CreditCard = creditCard;
    }

    public static PaymentMethod Create(Pse? pse, CreditCard? creditCard)
    {
        return new PaymentMethod(pse, creditCard);
    }
}
