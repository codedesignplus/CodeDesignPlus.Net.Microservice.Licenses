using System.Globalization;
using NodaTime.Text;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// Traduce una renovación pagada a las variables de la plantilla <c>SubscriptionRenewed</c> de ms-emails (pendings/735).
/// </summary>
/// <remarks>
/// Como <see cref="ReceiptEmailVariables"/>, es un adaptador de salida: el único sitio donde el importe vuelve a unidad
/// mayor, con los decimales de la moneda resueltos por quien llama (reglas 04 y 09). La fecha es la del calendario de la
/// copropiedad, no la de UTC (regla 11).
/// </remarks>
public static class RenewalEmailVariables
{
    private static readonly LocalDatePattern DatePattern = LocalDatePattern.CreateWithInvariantCulture("dd/MM/yyyy");

    /// <summary>
    /// Arma el diccionario que consume la plantilla.
    /// </summary>
    /// <param name="order">La renovación ya pagada.</param>
    /// <param name="decimalDigits">Los decimales de la moneda de la orden.</param>
    /// <returns>Las variables de la plantilla.</returns>
    public static Dictionary<string, string> Build(OrderAggregate order, short decimalDigits)
    {
        var zone = Domain.ServiceDomain.SubscriptionTimeline.ZoneOf(order.TenantDetail);
        var periodEnd = order.PeriodEnd ?? SystemClock.Instance.GetCurrentInstant();

        return new()
        {
            ["organization_name"] = order.TenantDetail.Name,
            ["license_name"] = order.License.Name,
            ["next_billing_date"] = DatePattern.Format(periodEnd.InZone(zone).Date),
            ["total"] = order.AmountToCharge.ToDecimal(decimalDigits).ToString("N2", CultureInfo.InvariantCulture),
            ["currency"] = order.AmountToCharge.Currency,
            ["current_year"] = SystemClock.Instance.GetCurrentInstant().InZone(zone).Year.ToString(CultureInfo.InvariantCulture),
        };
    }
}
