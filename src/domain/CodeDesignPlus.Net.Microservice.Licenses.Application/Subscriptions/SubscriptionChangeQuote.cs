using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions;

/// <summary>
/// La cotización de cambiarse a una licencia: si es mayor, su precio, el crédito por lo no prestado, el saldo a favor que
/// se aplica y lo que queda por pagar (pendings/738).
/// </summary>
/// <remarks>
/// La consultan las opciones de cambio y el propio cambio: el importe que se cobra es exactamente el que se cotizó, porque
/// sale del mismo cálculo (regla 06).
/// </remarks>
/// <param name="Target">La licencia del catálogo.</param>
/// <param name="Price">Su precio para la periodicidad de la copropiedad.</param>
/// <param name="Upgrade">Lo que gana con ella.</param>
/// <param name="CreditedPeriods">Lo no prestado de cada orden en curso.</param>
/// <param name="Charge">El cobro.</param>
public sealed record SubscriptionChangeQuote(LicenseAggregate Target, Price Price, LicenseUpgradeResult Upgrade, List<CreditedPeriod> CreditedPeriods, SubscriptionCharge Charge)
{
    /// <summary>
    /// Cotiza el cambio, o devuelve <c>null</c> si la licencia no se le puede ofrecer a la copropiedad: no está a la venta
    /// para ella, no tiene precio para su periodicidad o no es mayor que la que tiene.
    /// </summary>
    /// <param name="subscription">La suscripción de la copropiedad.</param>
    /// <param name="target">La licencia del catálogo.</param>
    /// <param name="creditBalance">El saldo a favor disponible.</param>
    /// <param name="now">El instante del cambio.</param>
    /// <returns>La cotización, o <c>null</c>.</returns>
    public static SubscriptionChangeQuote? For(SubscriptionTimeline subscription, LicenseAggregate target, Money creditBalance, Instant now)
    {
        var current = subscription.CurrentAt(now).License;

        if (!target.IsOfferedTo(subscription.TenantId))
        {
            return null;
        }

        var price = target.Prices.FirstOrDefault(x => x.BillingType == current.BillingType && x.Total.Currency == current.Total.Currency);

        if (price is null)
        {
            return null;
        }

        var upgrade = LicenseUpgrade.Compare(current, target);

        if (!upgrade.IsUpgrade)
        {
            return null;
        }

        var credited = Proration.Credit(subscription, now);
        var credit = credited.Aggregate(Money.Zero(price.Total.Currency), (sum, x) => sum + x.Total);

        return new SubscriptionChangeQuote(target, price, upgrade, credited, SubscriptionCharge.Compute(price.Total, credit, creditBalance));
    }
}
