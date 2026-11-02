using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;

/// <summary>
/// El periodo que paga una orden según la periodicidad de su precio (pendings/735).
/// </summary>
/// <remarks>
/// Mensual es un mes de calendario y anual, un año de calendario: es lo que significa la periodicidad, no un plazo que se
/// configure. Antes se sumaban 30 o 365 días, así que un periodo de febrero duraba más que el mes y uno de un año bisiesto
/// se quedaba un día corto.
/// </remarks>
public static class BillingPeriod
{
    /// <summary>
    /// El fin del periodo que empieza en <paramref name="start"/>.
    /// </summary>
    /// <param name="start">El inicio del periodo.</param>
    /// <param name="billingType">La periodicidad del precio.</param>
    /// <returns>El instante en que termina el periodo.</returns>
    /// <exception cref="CodeDesignPlus.Net.Exceptions.CodeDesignPlusException">Si la periodicidad no define un periodo.</exception>
    public static Instant End(Instant start, BillingType billingType)
    {
        DomainGuard.IsTrue(billingType == BillingType.None, Errors.BillingTypeHasNoPeriod);

        var local = start.InUtc().LocalDateTime;

        var end = billingType == BillingType.Monthly
            ? local.PlusMonths(1)
            : local.PlusYears(1);

        return end.InUtc().ToInstant();
    }
}
