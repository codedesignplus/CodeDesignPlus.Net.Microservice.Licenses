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
}
