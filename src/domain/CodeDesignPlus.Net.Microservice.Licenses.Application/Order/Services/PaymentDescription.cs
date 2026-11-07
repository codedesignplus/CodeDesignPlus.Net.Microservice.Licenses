using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// Lo que dice el cobro de una orden en ms-payments: qué se paga y para qué copropiedad, en español (pendings/674).
/// </summary>
/// <remarks>
/// <para>
/// Lo lee el Super Admin en Cobros de plataforma. Decía «Payment for license Élite to tenant …»: los textos de la orden,
/// como los del comprobante (<see cref="ReceiptText"/>), se escriben para el país del despliegue. La página de Wompi no lo
/// muestra: el adaptador de ms-payments no envía la descripción.
/// </para>
/// <para>
/// ms-payments la acepta hasta <see cref="MaxLength"/> caracteres; un nombre largo de copropiedad o de paquetes se corta con
/// «…» en vez de hacer fallar el cobro. Los cobros ya creados conservan la que tenían.
/// </para>
/// </remarks>
public static class PaymentDescription
{
    /// <summary>
    /// Lo más largo que acepta ms-payments (<c>InitiatePaymentCommandValidator</c>).
    /// </summary>
    public const int MaxLength = 255;

    /// <summary>
    /// La descripción del cobro de la orden: «Compra de la licencia Élite para Conjunto Las Acacias».
    /// </summary>
    /// <param name="order">La orden que se cobra.</param>
    /// <returns>La descripción, de <see cref="MaxLength"/> caracteres como mucho.</returns>
    public static string For(OrderAggregate order)
    {
        var tenant = order.TenantDetail.Name.Trim();
        var license = order.License.Name.Trim();
        var packages = (order.Packages ?? []).Select(x => x.Name.Trim()).ToList();

        var text = order.Type switch
        {
            OrderType.Purchase when packages.Count == 0 => $"Compra de la licencia {license} para {tenant}",
            OrderType.Purchase => $"Compra de la licencia {license} y {Count(packages.Count)} para {tenant}",
            OrderType.Renewal => $"Renovación de la licencia {license} para {tenant}",
            OrderType.Change => $"Cambio a la licencia {license} para {tenant}",
            OrderType.Package => $"{(packages.Count == 1 ? "Paquete" : "Paquetes")} {string.Join(", ", packages)} para {tenant}",
            _ => throw new ArgumentOutOfRangeException(nameof(order), order.Type, "The order type has no payment description.")
        };

        return text.Length <= MaxLength ? text : $"{text[..(MaxLength - 1)].TrimEnd()}…";
    }

    private static string Count(int packages) => packages == 1 ? "1 paquete" : $"{packages} paquetes";
}
