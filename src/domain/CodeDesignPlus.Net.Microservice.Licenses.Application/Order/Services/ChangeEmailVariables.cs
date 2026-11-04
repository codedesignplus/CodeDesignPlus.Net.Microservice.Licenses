using Currency = CodeDesignPlus.Net.ValueObjects.Financial.Currency;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// Traduce un cambio de licencia pagado a las variables de la plantilla <c>SubscriptionChanged</c> de ms-emails
/// (pendings/738).
/// </summary>
/// <remarks>
/// Antes el cambio usaba la confirmación de compra, que decía «¡Compra Confirmada!» con el precio completo como total y
/// prometía el comprobante en otro correo que no llegaba: el comprobante va adjunto a este. El correo del cambio dice el
/// precio, lo que se descontó de la licencia anterior y lo que de verdad se pagó, con los mismos importes del comprobante.
/// </remarks>
public static class ChangeEmailVariables
{
    /// <summary>
    /// La plantilla del correo de un cambio de licencia pagado.
    /// </summary>
    public const string Template = "SubscriptionChanged";

    /// <summary>
    /// La plantilla del correo de la compra o la ampliación de un paquete pagada (pendings/739).
    /// </summary>
    public const string PackageTemplate = "SubscriptionPackagePurchased";

    /// <summary>
    /// Arma el diccionario que consume la plantilla.
    /// </summary>
    /// <param name="order">El cambio ya pagado.</param>
    /// <param name="currency">La moneda de la orden, del catálogo de ms-locations: da el símbolo y los decimales.</param>
    /// <returns>Las variables del comprobante más la fecha de la próxima facturación.</returns>
    public static Dictionary<string, string> Build(OrderAggregate order, Currency currency)
    {
        var variables = ReceiptEmailVariables.Build(order, currency);
        var renewal = RenewalEmailVariables.Build(order, currency);

        variables["next_billing_date"] = renewal["next_billing_date"];

        return variables;
    }

    /// <summary>
    /// Arma el diccionario que consume <see cref="PackageTemplate"/>: solo lo que el correo muestra del paquete (pendings/739).
    /// </summary>
    /// <param name="order">La compra o la ampliación del paquete ya pagada.</param>
    /// <param name="currency">La moneda de la orden, del catálogo de ms-locations: da el símbolo y los decimales.</param>
    /// <returns>El paquete, su vigencia, su precio, el crédito aplicado y lo pagado, con los importes del comprobante.</returns>
    /// <remarks>
    /// Antes llevaba las variables de la licencia con valores falsos («subtotal» $ 0, «IVA (0 %)», los módulos), porque la
    /// orden de un paquete lleva una copia de la licencia con precio cero. La plantilla llama <c>license_name</c> al paquete.
    /// </remarks>
    public static Dictionary<string, string> BuildPackage(OrderAggregate order, Currency currency)
    {
        var receipt = ReceiptEmailVariables.Build(order, currency);

        return new Dictionary<string, string>
        {
            ["organization_name"] = receipt["organization_name"],
            ["license_name"] = receipt["package_name"],
            ["period"] = receipt["package_period"],
            ["price"] = ReceiptText.Amount(order.Total, currency),
            ["credit_applied"] = receipt["credit_applied"],
            ["credit_row_display"] = receipt["credit_row_display"],
            ["amount_paid"] = receipt["amount_paid"],
            ["current_year"] = receipt["current_year"]
        };
    }
}
