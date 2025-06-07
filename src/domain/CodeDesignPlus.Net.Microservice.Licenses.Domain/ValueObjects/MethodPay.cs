using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class MethodPay
{
    public Pse? Pse { get; private set; }
    public CreditCard? CreditCard { get; private set; }

    [JsonConstructor]
    private MethodPay(Pse? pse, CreditCard? creditCard)
    {
        if(pse == null)
            DomainGuard.IsNull(creditCard!, Errors.CreditCardCannotBeNull);

        if(creditCard == null)
            DomainGuard.IsNull(pse!, Errors.PseCannotBeNull);

        this.Pse = pse;
        this.CreditCard = creditCard;
    }

    public static MethodPay Create(Pse? pse, CreditCard? creditCard)
    {
        return new MethodPay(pse, creditCard);
    }
}
