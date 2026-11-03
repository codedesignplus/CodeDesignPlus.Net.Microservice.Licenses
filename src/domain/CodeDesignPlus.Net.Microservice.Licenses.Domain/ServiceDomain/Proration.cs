using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;

/// <summary>
/// Lo que no se presta de la licencia vigente cuando la copropiedad cambia a otra (pendings/738).
/// </summary>
/// <remarks>
/// Por cada orden que paga la suscripción:
/// <list type="bullet">
/// <item>la del periodo en curso acredita su base facturada sin impuestos por los días que faltan sobre los días de su
/// periodo, contados con el inicio y el fin guardados, en la zona de la copropiedad, y el IVA de esa base con la tarifa
/// de su factura: si la factura llevó IVA, la nota crédito lleva el IVA proporcional (experto contable, 2026-11-02);</item>
/// <item>una renovación pagada que aún no empieza se acredita entera;</item>
/// <item>un periodo ya terminado no acredita nada.</item>
/// </list>
/// El día del cambio cuenta como no prestado, porque el periodo nuevo empieza ese mismo día, y el importe se redondea hacia
/// arriba en unidades mínimas: las dos cosas, a favor del cliente.
/// </remarks>
public static class Proration
{
    /// <summary>
    /// Lo no prestado de cada orden de la suscripción.
    /// </summary>
    /// <param name="subscription">La suscripción.</param>
    /// <param name="now">El instante del cambio.</param>
    /// <returns>Un periodo acreditado por cada orden que aún tiene algo por prestar.</returns>
    public static List<CreditedPeriod> Credit(SubscriptionTimeline subscription, Instant now)
    {
        var today = subscription.Today(now);
        var credited = new List<CreditedPeriod>();

        foreach (var order in subscription.Orders)
        {
            var start = order.PeriodStart!.Value;
            var end = order.PeriodEnd!.Value;

            if (end <= now)
            {
                continue;
            }

            var startDate = start.InZone(subscription.Zone).Date;
            var endDate = end.InZone(subscription.Zone).Date;
            var periodDays = Math.Max(Period.DaysBetween(startDate, endDate), 1);

            var notStarted = start > now;
            var unusedDays = notStarted ? periodDays : Math.Clamp(Period.DaysBetween(today, endDate), 0, periodDays);

            if (unusedDays == 0)
            {
                continue;
            }

            var from = notStarted ? start : today.AtStartOfDayInZone(subscription.Zone).ToInstant();
            var amount = Prorate(order.License.SubTotal, unusedDays, periodDays);
            var tax = ProrateTax(order.License, amount, unusedDays, periodDays);

            credited.Add(CreditedPeriod.Create(order.Id, from, end, unusedDays, periodDays, order.License.SubTotal, amount, tax, order.License.TaxBasisPoints));
        }

        return credited;
    }

    /// <summary>
    /// La base por los días no prestados sobre los días del periodo, redondeada hacia arriba en unidades mínimas.
    /// </summary>
    /// <param name="base">La base facturada sin impuestos.</param>
    /// <param name="unusedDays">Los días no prestados.</param>
    /// <param name="periodDays">Los días del periodo.</param>
    /// <returns>El crédito.</returns>
    public static Money Prorate(Money @base, int unusedDays, int periodDays)
    {
        DomainGuard.IsTrue(periodDays <= 0 || unusedDays < 0 || unusedDays > periodDays, Errors.ProrationDaysAreInvalid);

        // Entero y hacia arriba: redondeando al más cercano, una fracción de centavo se quedaba del lado de la plataforma.
        var numerator = @base.Amount * unusedDays;
        var amount = numerator / periodDays + (numerator % periodDays == 0 ? 0 : 1);

        return Money.FromLong(amount, @base.Currency);
    }

    /// <summary>
    /// El IVA de la base acreditada con la tarifa de la factura de la orden, redondeado hacia arriba y sin pasar del IVA
    /// facturado.
    /// </summary>
    /// <param name="license">La copia de la licencia de la orden acreditada.</param>
    /// <param name="credited">La base acreditada.</param>
    /// <param name="unusedDays">Los días no prestados.</param>
    /// <param name="periodDays">Los días del periodo.</param>
    /// <returns>El IVA que se acredita; cero si la factura no llevó IVA.</returns>
    public static Money ProrateTax(License license, Money credited, int unusedDays, int periodDays)
    {
        if (license.Tax.Amount == 0)
        {
            return Money.Zero(license.Tax.Currency);
        }

        if (license.TaxBasisPoints <= 0)
        {
            // Una orden comprada antes de guardar la tarifa (pendings/662): su IVA, en la misma proporción de los días.
            return Prorate(license.Tax, unusedDays, periodDays);
        }

        // Puntos básicos: 10.000 es el ciento por ciento (regla 05).
        var numerator = credited.Amount * license.TaxBasisPoints;
        var amount = numerator / 10_000 + (numerator % 10_000 == 0 ? 0 : 1);

        return Money.FromLong(Math.Min(amount, license.Tax.Amount), license.Tax.Currency);
    }
}
