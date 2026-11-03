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
            var period = CreditOf(order.Id, order.PeriodStart!.Value, order.PeriodEnd!.Value, order.License.SubTotal, order.License.Tax, order.License.TaxBasisPoints, subscription.Zone, today, now);

            if (period is not null)
            {
                credited.Add(period);
            }
        }

        return credited;
    }

    /// <summary>
    /// Lo no usado de una línea de paquete, con la misma regla que la licencia: la nota crédito de una ampliación o de la
    /// recotización de un cambio (pendings/739).
    /// </summary>
    /// <param name="orderId">La orden de la línea.</param>
    /// <param name="line">La línea con su periodo.</param>
    /// <param name="zone">La zona horaria de la copropiedad.</param>
    /// <param name="now">El instante de la ampliación o del cambio.</param>
    /// <returns>El periodo acreditado de la línea, o <c>null</c> si ya no tiene nada por prestar.</returns>
    public static CreditedPeriod? CreditLine(Guid orderId, PackageLine line, DateTimeZone zone, Instant now)
    {
        DomainGuard.IsTrue(line.PeriodStart is null || line.PeriodEnd is null, Errors.OrderHasNoPeriod);

        var today = now.InZone(zone).Date;

        return CreditOf(orderId, line.PeriodStart!.Value, line.PeriodEnd!.Value, line.SubTotal, line.Tax, line.TaxBasisPoints, zone, today, now)?.ForLine(line.LineId);
    }

    private static CreditedPeriod? CreditOf(Guid orderId, Instant start, Instant end, Money subTotal, Money invoicedTax, int taxBasisPoints, DateTimeZone zone, LocalDate today, Instant now)
    {
        if (end <= now)
        {
            return null;
        }

        var startDate = start.InZone(zone).Date;
        var endDate = end.InZone(zone).Date;
        var periodDays = Math.Max(Period.DaysBetween(startDate, endDate), 1);

        var notStarted = start > now;
        var unusedDays = notStarted ? periodDays : Math.Clamp(Period.DaysBetween(today, endDate), 0, periodDays);

        if (unusedDays == 0)
        {
            return null;
        }

        var from = notStarted ? start : today.AtStartOfDayInZone(zone).ToInstant();
        var amount = Prorate(subTotal, unusedDays, periodDays);
        var tax = ProrateTax(invoicedTax, taxBasisPoints, amount, unusedDays, periodDays);

        return CreditedPeriod.Create(orderId, from, end, unusedDays, periodDays, subTotal, amount, tax, taxBasisPoints);
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
    public static Money ProrateTax(License license, Money credited, int unusedDays, int periodDays) => ProrateTax(license.Tax, license.TaxBasisPoints, credited, unusedDays, periodDays);

    /// <summary>
    /// El IVA de la base acreditada con la tarifa facturada, redondeado hacia arriba y sin pasar del IVA facturado.
    /// </summary>
    /// <param name="invoicedTax">El IVA facturado.</param>
    /// <param name="taxBasisPoints">La tarifa facturada.</param>
    /// <param name="credited">La base acreditada.</param>
    /// <param name="unusedDays">Los días no prestados.</param>
    /// <param name="periodDays">Los días del periodo.</param>
    /// <returns>El IVA que se acredita; cero si la factura no llevó IVA.</returns>
    public static Money ProrateTax(Money invoicedTax, int taxBasisPoints, Money credited, int unusedDays, int periodDays)
    {
        if (invoicedTax.Amount == 0)
        {
            return Money.Zero(invoicedTax.Currency);
        }

        if (taxBasisPoints <= 0)
        {
            // Una orden comprada antes de guardar la tarifa (pendings/662): su IVA, en la misma proporción de los días.
            return Prorate(invoicedTax, unusedDays, periodDays);
        }

        // Puntos básicos: 10.000 es el ciento por ciento (regla 05).
        var numerator = credited.Amount * taxBasisPoints;
        var amount = numerator / 10_000 + (numerator % 10_000 == 0 ? 0 : 1);

        return Money.FromLong(Math.Min(amount, invoicedTax.Amount), invoicedTax.Currency);
    }
}
