using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;

/// <summary>
/// Cuánto se cobra de una renovación o de un cambio de licencia una vez descontados el crédito por lo no prestado y el
/// saldo a favor (pendings/735, 738).
/// </summary>
/// <remarks>
/// El precio de la licencia es el que se cobra y el que dice la factura: el crédito y el saldo a favor no son un descuento,
/// son un anticipo aplicado. Primero se aplica el crédito del cambio y después el saldo a favor; lo que sobra del crédito
/// queda como saldo a favor para las renovaciones siguientes.
/// </remarks>
/// <param name="Total">El precio completo, con impuestos.</param>
/// <param name="ProrationCredit">El crédito por lo no prestado de la licencia anterior.</param>
/// <param name="CreditApplied">La parte del crédito que paga este total.</param>
/// <param name="CreditBalanceApplied">La parte del saldo a favor que paga este total.</param>
/// <param name="AmountDue">Lo que queda por pagar en la pasarela.</param>
/// <param name="RemainingCreditBalance">El saldo a favor que quedará después del pago.</param>
public sealed record SubscriptionCharge(Money Total, Money ProrationCredit, Money CreditApplied, Money CreditBalanceApplied, Money AmountDue, Money RemainingCreditBalance)
{
    /// <summary>
    /// Calcula el cobro.
    /// </summary>
    /// <param name="total">El precio completo, con impuestos.</param>
    /// <param name="prorationCredit">El crédito por lo no prestado; cero en una renovación.</param>
    /// <param name="creditBalance">El saldo a favor disponible de la copropiedad.</param>
    /// <returns>El cobro.</returns>
    public static SubscriptionCharge Compute(Money total, Money prorationCredit, Money creditBalance)
    {
        DomainGuard.IsTrue(prorationCredit.Amount < 0 || creditBalance.Amount < 0, Errors.CreditCannotBeNegative);

        var creditApplied = Money.Min(prorationCredit, total);
        var balanceApplied = Money.Min(creditBalance, total - creditApplied);
        var amountDue = total - creditApplied - balanceApplied;
        var remaining = prorationCredit - creditApplied + creditBalance - balanceApplied;

        return new SubscriptionCharge(total, prorationCredit, creditApplied, balanceApplied, amountDue, remaining);
    }
}
