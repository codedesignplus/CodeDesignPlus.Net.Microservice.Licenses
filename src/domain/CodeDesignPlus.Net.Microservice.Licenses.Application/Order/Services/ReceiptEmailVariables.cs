using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using NodaTime.Text;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// Traduce una orden a las variables de las plantillas <c>PurchaseConfirmation</c> y <c>PurchaseReceipt</c> de ms-emails.
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
/// <para>
/// Todo lo que sale de aquí lo lee el comprador tal cual, así que ningún valor puede ser el <c>ToString()</c> de un
/// objeto o de un enum (pendings/264): el documento es «NIT 900123456», la periodicidad va en el idioma de la plantilla
/// y la fecha es la del calendario de la copropiedad, no la de UTC (regla 11).
/// </para>
/// </remarks>
public static class ReceiptEmailVariables
{
    private static readonly LocalDatePattern DatePattern = LocalDatePattern.CreateWithInvariantCulture("dd/MM/yyyy");

    /// <summary>
    /// Arma el diccionario que consume la plantilla.
    /// </summary>
    /// <param name="order">La orden ya pagada.</param>
    /// <param name="decimalDigits">Los decimales de la moneda de la orden, resueltos por el llamador.</param>
    /// <returns>Las variables de la plantilla, con los importes ya legibles.</returns>
    public static Dictionary<string, string> Build(OrderAggregate order, short decimalDigits)
    {
        var zone = SubscriptionTimeline.ZoneOf(order.TenantDetail);
        var purchasedAt = PurchasedAt(order).InZone(zone);

        return new()
        {
            ["organization_name"] = order.TenantDetail.Name,
            ["organization_email"] = order.TenantDetail.Email,
            ["organization_phone"] = order.TenantDetail.Phone,
            ["organization_document"] = $"{order.TenantDetail.TypeDocument.Code} {order.TenantDetail.NumberDocument}",
            ["buyer_name"] = order.Buyer.Name,
            ["buyer_email"] = order.Buyer.Email,
            ["license_name"] = order.License.Name,
            ["billing_type"] = BillingTypeLabel(order.License.BillingType),
            ["subtotal"] = order.License.SubTotal.ToDecimal(decimalDigits).ToString("N2"),
            ["tax"] = order.License.Tax.ToDecimal(decimalDigits).ToString("N2"),
            ["total"] = order.License.Total.ToDecimal(decimalDigits).ToString("N2"),
            ["currency"] = order.License.Total.Currency,
            ["purchase_date"] = DatePattern.Format(purchasedAt.Date),
            ["current_year"] = purchasedAt.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["order_id"] = order.Id.ToString(),
            ["modules_list"] = string.Join(", ", order.License.Modules.Select(m => m.Name))
        };
    }

    /// <summary>
    /// El instante en que se aprobó el pago; si la orden no lo registra, ahora, que es cuando se arma el recibo.
    /// </summary>
    private static Instant PurchasedAt(OrderAggregate order)
    {
        var payment = order.ProvisioningHistory
            .LastOrDefault(x => x.StepName == ProvisioningStep.Payment && x.Status == ProvisioningStepStatus.Completed);

        return payment?.Timestamp ?? SystemClock.Instance.GetCurrentInstant();
    }

    /// <summary>
    /// La periodicidad en el idioma de las plantillas, que hoy están escritas en español.
    /// </summary>
    /// <remarks>
    /// No es un catálogo: son las dos periodicidades que <see cref="BillingPeriod"/> sabe cobrar, y una nueva exige
    /// código ahí antes que una etiqueta aquí. <see cref="BillingType.None"/> no llega: una orden sin periodo no se paga.
    /// </remarks>
    private static string BillingTypeLabel(BillingType billingType) => billingType switch
    {
        BillingType.Monthly => "Mensual",
        BillingType.Annually => "Anual",
        _ => throw new ArgumentOutOfRangeException(nameof(billingType), billingType, "The billing type has no period to charge.")
    };
}
