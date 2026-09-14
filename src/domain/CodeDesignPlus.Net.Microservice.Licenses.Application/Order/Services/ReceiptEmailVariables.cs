namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// Traduce una orden a las variables de la plantilla del correo de compra.
/// </summary>
/// <remarks>
/// Esto es un <b>adaptador de salida</b>, y por eso vive fuera de <c>Commands/</c>. Es el unico sitio donde
/// un importe vuelve a unidad mayor: un command recibe dinero y no lo devuelve, asi que convertir dentro del
/// manejador era deshacer lo que se acababa de recibir (regla 04). La plantilla del correo si necesita
/// "1.234,56" porque la lee una persona, y esa traduccion pertenece a quien habla con el correo, no al caso
/// de uso.
/// <para>
/// La conversion sigue necesitando los decimales de la moneda y no se asumen dos: llegan resueltos desde
/// <c>ICurrencyGrpc</c> (regla 09).
/// </para>
/// </remarks>
public static class ReceiptEmailVariables
{
    /// <summary>
    /// Arma el diccionario que consume la plantilla.
    /// </summary>
    /// <param name="order">La orden ya pagada.</param>
    /// <param name="decimalDigits">Los decimales de la moneda de la orden, resueltos por el llamador.</param>
    /// <returns>Las variables de la plantilla, con los importes ya legibles.</returns>
    public static Dictionary<string, string> Build(OrderAggregate order, short decimalDigits) => new()
    {
        ["organization_name"] = order.TenantDetail.Name,
        ["organization_email"] = order.TenantDetail.Email,
        ["organization_phone"] = order.TenantDetail.Phone,
        ["organization_document"] = $"{order.TenantDetail.TypeDocument}: {order.TenantDetail.NumberDocument}",
        ["buyer_name"] = order.Buyer.Name,
        ["buyer_email"] = order.Buyer.Email,
        ["license_name"] = order.License.Name,
        ["billing_type"] = order.License.BillingType.ToString(),
        ["subtotal"] = order.License.SubTotal.ToDecimal(decimalDigits).ToString("N2"),
        ["tax"] = order.License.Tax.ToDecimal(decimalDigits).ToString("N2"),
        ["total"] = order.License.Total.ToDecimal(decimalDigits).ToString("N2"),
        ["currency"] = order.License.Total.Currency,
        ["purchase_date"] = SystemClock.Instance.GetCurrentInstant().ToString(),
        ["current_year"] = DateTime.UtcNow.Year.ToString(),
        ["order_id"] = order.Id.ToString(),
        ["modules_list"] = string.Join(", ", order.License.Modules.Select(m => m.Name))
    };
}
