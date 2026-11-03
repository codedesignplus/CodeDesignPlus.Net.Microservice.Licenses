using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;

/// <summary>
/// Cuánto dura un paquete y qué paquetes cuentan en un momento (pendings/739).
/// </summary>
/// <remarks>
/// <para>
/// El precio de un paquete es anual y se cobra completo. Con licencia <b>anual</b> el paquete vence con la licencia (su
/// periodo es el de la licencia, o lo que falte de él si se compra a mitad) y se renueva con ella. Con licencia
/// <b>mensual</b> dura un año de calendario desde la compra, sin renovarse con cada mes (decisión del usuario, 2026-11-03).
/// No es un plazo configurable: es lo que significa que el precio sea anual.
/// </para>
/// <para>
/// Un paquete solo cuenta mientras la licencia esté pagada: si vence y no se renueva, sus atributos dejan de contar y
/// vuelven al pagarla, sin alargar su periodo (precisión del usuario, 2026-11-03). Una línea deja de contar en cuanto empieza
/// la que la reemplaza (la ampliación, la renovación o la recotización de un cambio).
/// </para>
/// </remarks>
public static class PackageTerm
{
    /// <summary>
    /// Si los paquetes van con el periodo de la licencia: solo con licencia anual.
    /// </summary>
    /// <param name="licenseBillingType">La periodicidad de la licencia.</param>
    /// <returns><c>true</c> si vencen y se renuevan con la licencia.</returns>
    public static bool IsCoterminous(BillingType licenseBillingType) => licenseBillingType == BillingType.Annually;

    /// <summary>
    /// El fin del periodo de una línea que empieza en <paramref name="start"/>.
    /// </summary>
    /// <param name="start">El inicio.</param>
    /// <param name="licenseBillingType">La periodicidad de la licencia.</param>
    /// <param name="licensePeriodEnd">El fin del periodo de la licencia al que se ata con licencia anual.</param>
    /// <returns>El fin.</returns>
    public static Instant End(Instant start, BillingType licenseBillingType, Instant? licensePeriodEnd)
    {
        if (IsCoterminous(licenseBillingType) && licensePeriodEnd is not null)
        {
            return licensePeriodEnd.Value < start ? start : licensePeriodEnd.Value;
        }

        return BillingPeriod.End(start, BillingType.Annually);
    }

    /// <summary>
    /// Las líneas de paquete pagadas de una copropiedad, con su orden.
    /// </summary>
    /// <param name="orders">Las órdenes pagadas de la copropiedad, activas o no: un cambio de licencia inactiva la orden de
    /// la compra, pero sus paquetes siguen si no se recotizaron.</param>
    /// <returns>Las líneas con periodo, por inicio.</returns>
    public static List<(OrderAggregate Order, PackageLine Line)> PaidLines(IEnumerable<OrderAggregate> orders) => [.. orders
        .Where(x => x.PaymentStatus == PaymentStatus.Succeeded && x.SupersededBy is null && !x.LicenseApplicationFailed)
        .SelectMany(order => (order.Packages ?? []).Where(line => line.PeriodStart is not null && line.PeriodEnd is not null).Select(line => (order, line)))
        .OrderBy(x => x.line.PeriodStart)];

    /// <summary>
    /// Las líneas vigentes en un instante: en su periodo y sin una línea que las reemplace ya empezada. No mira la licencia.
    /// </summary>
    /// <param name="lines">Las líneas pagadas.</param>
    /// <param name="now">El instante.</param>
    /// <returns>Las líneas vigentes.</returns>
    public static List<(OrderAggregate Order, PackageLine Line)> Current(IReadOnlyCollection<(OrderAggregate Order, PackageLine Line)> lines, Instant now)
    {
        var replaced = lines
            .Where(x => x.Line.ReplacesLineId is not null && x.Line.PeriodStart!.Value <= now)
            .Select(x => x.Line.ReplacesLineId!.Value)
            .ToHashSet();

        return [.. lines.Where(x => x.Line.CoversAt(now) && !replaced.Contains(x.Line.LineId))];
    }

    /// <summary>
    /// Las líneas ya pagadas que empiezan después (una renovación pagada por adelantado).
    /// </summary>
    /// <param name="lines">Las líneas pagadas.</param>
    /// <param name="now">El instante.</param>
    /// <returns>Las líneas futuras.</returns>
    public static List<(OrderAggregate Order, PackageLine Line)> Upcoming(IReadOnlyCollection<(OrderAggregate Order, PackageLine Line)> lines, Instant now) =>
        [.. lines.Where(x => x.Line.PeriodStart!.Value > now)];

    /// <summary>
    /// Los atributos efectivos de la copropiedad: los de la licencia más los de las líneas vigentes si la licencia está
    /// pagada en ese instante.
    /// </summary>
    /// <param name="licenseAttributes">Los atributos de la licencia en curso.</param>
    /// <param name="paidThrough">Hasta cuándo está pagada la licencia.</param>
    /// <param name="lines">Las líneas pagadas.</param>
    /// <param name="now">El instante.</param>
    /// <returns>Los atributos efectivos.</returns>
    public static Dictionary<string, string> EffectiveAttributes(IReadOnlyDictionary<string, string> licenseAttributes, Instant paidThrough, IReadOnlyCollection<(OrderAggregate Order, PackageLine Line)> lines, Instant now)
    {
        var counting = now < paidThrough ? Current(lines, now) : [];

        return AttributeCombiner.Combine(licenseAttributes, counting.SelectMany(x => x.Line.Attributes));
    }
}
