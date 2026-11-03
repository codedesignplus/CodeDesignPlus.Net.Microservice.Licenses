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
    /// Los paquetes vigentes recotizados al periodo nuevo, con licencia anual (pendings/739). Vacío con licencia mensual.
    /// </summary>
    public List<PackageQuote> PackageQuotes { get; init; } = [];

    /// <summary>
    /// Las líneas de esos paquetes para la orden del cambio, cada una reemplazando a la vigente.
    /// </summary>
    public List<PackageLine> PackageLines { get; init; } = [];

    /// <summary>
    /// Cotiza el cambio, o devuelve <c>null</c> si la licencia no se le puede ofrecer a la copropiedad: no está a la venta
    /// para ella, no tiene precio para su periodicidad o no es mayor que la que tiene.
    /// </summary>
    /// <param name="subscription">La suscripción de la copropiedad.</param>
    /// <param name="target">La licencia del catálogo.</param>
    /// <param name="creditBalance">El saldo a favor disponible.</param>
    /// <param name="now">El instante del cambio.</param>
    /// <param name="packages">Los paquetes recotizados y lo no usado de cada uno (pendings/739); nada si no tiene.</param>
    /// <returns>La cotización, o <c>null</c>.</returns>
    public static SubscriptionChangeQuote? For(SubscriptionTimeline subscription, LicenseAggregate target, Money creditBalance, Instant now, (List<PackageQuote> Quotes, List<PackageLine> Lines, List<CreditedPeriod> Credited)? packages = null)
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
        var quotes = packages?.Quotes ?? [];
        var lines = packages?.Lines ?? [];

        credited.AddRange(packages?.Credited ?? []);

        var credit = credited.Aggregate(Money.Zero(price.Total.Currency), (sum, x) => sum + x.Total);
        var total = quotes.Aggregate(price.Total, (sum, x) => sum + x.Total);

        return new SubscriptionChangeQuote(target, price, upgrade, credited, SubscriptionCharge.Compute(total, credit, creditBalance)) { PackageQuotes = quotes, PackageLines = lines };
    }
}
