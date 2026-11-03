using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using NodaTime.Text;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// Traduce una orden a las variables de las plantillas <c>PurchaseConfirmation</c>, <c>PurchaseReceipt</c> y
/// <c>SubscriptionReceipt</c> de ms-emails.
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
/// <para>
/// Una renovación o un cambio de licencia no cobra siempre el precio: el crédito por lo no usado y el saldo a favor pagan
/// una parte o todo (pendings/738). Su recibo es <see cref="SubscriptionReceiptTemplate"/>, que muestra el precio, el
/// crédito aplicado y el total pagado; el de compra no lo necesita, porque una compra se paga completa.
/// </para>
/// </remarks>
public static class ReceiptEmailVariables
{
    /// <summary>
    /// La plantilla del recibo de una compra.
    /// </summary>
    public const string PurchaseReceiptTemplate = "PurchaseReceipt";

    /// <summary>
    /// La plantilla del recibo de una renovación o un cambio de licencia: precio, crédito aplicado y total pagado.
    /// </summary>
    public const string SubscriptionReceiptTemplate = "SubscriptionReceipt";

    private static readonly LocalDatePattern DatePattern = LocalDatePattern.CreateWithInvariantCulture("dd/MM/yyyy");

    /// <summary>
    /// La plantilla del recibo de la orden.
    /// </summary>
    /// <param name="order">La orden ya pagada.</param>
    /// <returns>El nombre de la plantilla en ms-emails.</returns>
    public static string ReceiptTemplateOf(OrderAggregate order) => order.Type == OrderType.Purchase ? PurchaseReceiptTemplate : SubscriptionReceiptTemplate;

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
            ["tax_label"] = TaxLabel(order.License),
            ["tax_rate"] = TaxRate(order.License.TaxBasisPoints),
            ["tax_legal_basis"] = order.License.TaxLegalBasis ?? string.Empty,
            ["invoice_concept"] = order.License.InvoiceConcept ?? order.License.Name,
            ["total"] = order.License.Total.ToDecimal(decimalDigits).ToString("N2"),
            ["price"] = order.License.Total.ToDecimal(decimalDigits).ToString("N2"),
            ["credit_applied"] = (order.License.Total - order.AmountToCharge).ToDecimal(decimalDigits).ToString("N2"),
            ["amount_paid"] = order.AmountToCharge.ToDecimal(decimalDigits).ToString("N2"),
            ["order_type"] = OrderTypeLabel(order.Type),
            ["currency"] = order.License.Total.Currency,
            ["purchase_date"] = DatePattern.Format(purchasedAt.Date),
            ["current_year"] = purchasedAt.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["order_id"] = order.Id.ToString(),
            ["modules_list"] = string.Join(", ", order.License.Modules.Select(m => m.Name))
        };
    }

    /// <summary>
    /// La fila del impuesto en el recibo según el tratamiento del precio comprado (pendings/662): «IVA (19 %)» si es
    /// gravado, «Excluido de IVA» o «Exento de IVA» si no. Antes la plantilla decía «IVA (19%)» fijo.
    /// </summary>
    /// <remarks>
    /// El impuesto del precio de una licencia es el IVA: es el único que cobra la plataforma por la suscripción. No es un
    /// catálogo: son los tres tratamientos que <see cref="TaxTreatment"/> sabe validar, y uno nuevo exige código ahí antes
    /// que una etiqueta aquí.
    /// </remarks>
    private static string TaxLabel(Domain.ValueObjects.License license) => license.TaxTreatment switch
    {
        TaxTreatment.Taxed => $"IVA ({TaxRate(license.TaxBasisPoints)})",
        TaxTreatment.Excluded => "Excluido de IVA",
        TaxTreatment.Exempt => "Exento de IVA",
        _ => throw new ArgumentOutOfRangeException(nameof(license), license.TaxTreatment, "The tax treatment has no label.")
    };

    /// <summary>
    /// La tarifa legible: 1900 puntos básicos son «19 %» y 1650 son «16,5 %», con la coma decimal de las plantillas.
    /// </summary>
    /// <remarks>
    /// Se formatea con la cultura invariante y se cambia el punto por la coma: el contenedor puede correr sin datos de
    /// culturas, y pedir «es-CO» ahí falla.
    /// </remarks>
    private static string TaxRate(int basisPoints) => $"{(basisPoints / 100m).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',')} %";

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
    /// Qué pagó la orden, en el idioma de las plantillas.
    /// </summary>
    /// <remarks>
    /// No es un catálogo: son los tres tipos de orden que el micro sabe cobrar, y uno nuevo exige código antes que una
    /// etiqueta.
    /// </remarks>
    private static string OrderTypeLabel(OrderType type) => type switch
    {
        OrderType.Purchase => "Compra",
        OrderType.Renewal => "Renovación",
        OrderType.Change => "Cambio de licencia",
        _ => throw new ArgumentOutOfRangeException(nameof(type), type, "The order type has no label.")
    };

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
