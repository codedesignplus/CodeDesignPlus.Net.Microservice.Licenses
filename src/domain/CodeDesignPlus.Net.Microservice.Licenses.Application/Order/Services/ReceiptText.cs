using System.Globalization;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// Cómo se leen un importe, un periodo y el nombre del comprobante en los correos y recibos de licencias (pendings/738).
/// </summary>
/// <remarks>
/// <para>
/// Las plantillas están escritas en español y para el país del despliegue, así que un importe se escribe como en sus
/// pantallas: «$ 773.500,00», con el símbolo y los decimales del catálogo de monedas (regla 09), punto de miles y coma
/// decimal. Antes salía «773,500.00 COP», el formato de la cultura invariante.
/// </para>
/// <para>
/// Los separadores y los meses van escritos aquí y no con <c>CultureInfo("es-CO")</c>: el micro corre con
/// InvariantGlobalization y esa cultura lanza <see cref="CultureNotFoundException"/>. Es la misma solución de la tarifa del
/// IVA de <see cref="ReceiptEmailVariables"/> y de los avisos de los micros de Kappali.
/// </para>
/// </remarks>
public static class ReceiptText
{
    private static readonly string[] Months = ["ene", "feb", "mar", "abr", "may", "jun", "jul", "ago", "sep", "oct", "nov", "dic"];

    /// <summary>
    /// Un importe como lo lee el comprador: «$ 773.500,00».
    /// </summary>
    /// <param name="amount">El importe en unidades mínimas.</param>
    /// <param name="currency">La moneda del importe, del catálogo de ms-locations: da el símbolo y los decimales.</param>
    /// <returns>El importe legible; un negativo lleva el signo delante del símbolo.</returns>
    public static string Amount(Money amount, Currency currency)
    {
        var value = amount.ToDecimal(currency.DecimalDigits);
        var invariant = Math.Abs(value).ToString($"N{currency.DecimalDigits}", CultureInfo.InvariantCulture);
        var digits = string.Concat(invariant.Select(x => x switch { ',' => '.', '.' => ',', _ => x }));
        var sign = value < 0 ? "-" : string.Empty;

        return $"{sign}{currency.Symbol} {digits}";
    }

    /// <summary>
    /// Una fecha de calendario como la muestran las pantallas: «28 de nov de 2026».
    /// </summary>
    /// <param name="date">La fecha, ya en el calendario de la copropiedad.</param>
    /// <returns>La fecha legible.</returns>
    public static string Date(LocalDate date) => $"{date.Day} de {Months[date.Month - 1]} de {date.Year}";

    /// <summary>
    /// El periodo que paga la orden, con las fechas del calendario de la copropiedad (regla 11): «28 de nov de 2026 — 28 de
    /// dic de 2026».
    /// </summary>
    /// <param name="order">La orden ya pagada.</param>
    /// <param name="zone">La zona de la copropiedad.</param>
    /// <returns>El periodo, o vacío si la orden aún no lo tiene.</returns>
    public static string Period(OrderAggregate order, DateTimeZone zone)
    {
        if (order.PeriodStart is null || order.PeriodEnd is null)
        {
            return string.Empty;
        }

        return $"{Date(order.PeriodStart.Value.InZone(zone).Date)} — {Date(order.PeriodEnd.Value.InZone(zone).Date)}";
    }

    /// <summary>
    /// El nombre del PDF del comprobante según lo que pagó la orden: «ComprobanteRenovacion-{id}.pdf». Sin tildes, porque
    /// viaja como nombre de archivo adjunto.
    /// </summary>
    /// <param name="order">La orden ya pagada.</param>
    /// <returns>El nombre del archivo.</returns>
    /// <remarks>
    /// No es un catálogo: son los tres tipos de orden que el micro sabe cobrar, y uno nuevo exige código antes que un
    /// nombre. Antes todos se llamaban <c>PurchaseReceipt.pdf</c>, también una renovación o un cambio.
    /// </remarks>
    public static string ReceiptFileName(OrderAggregate order) => order.Type switch
    {
        OrderType.Purchase => $"ComprobanteCompra-{order.Id}.pdf",
        OrderType.Renewal => $"ComprobanteRenovacion-{order.Id}.pdf",
        OrderType.Change => $"ComprobanteCambio-{order.Id}.pdf",
        _ => throw new ArgumentOutOfRangeException(nameof(order), order.Type, "The order type has no receipt name.")
    };
}
