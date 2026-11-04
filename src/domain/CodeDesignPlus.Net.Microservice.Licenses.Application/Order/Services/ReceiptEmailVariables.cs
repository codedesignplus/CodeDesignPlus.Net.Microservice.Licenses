using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;
using NodaTime.Text;
using Currency = CodeDesignPlus.Net.ValueObjects.Financial.Currency;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// Traduce una orden a las variables de las plantillas <c>PurchaseConfirmation</c>, <c>PurchaseReceipt</c>,
/// <c>SubscriptionReceipt</c> y <c>PackageReceipt</c> de ms-emails.
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

    /// <summary>
    /// La plantilla del recibo de la compra o la ampliación de un paquete: el paquete con su base, su impuesto, su total y su
    /// vigencia, sin los módulos de la licencia, que no se compran (pendings/739).
    /// </summary>
    public const string PackageReceiptTemplate = "PackageReceipt";

    private static readonly LocalDatePattern DatePattern = LocalDatePattern.CreateWithInvariantCulture("dd/MM/yyyy");

    /// <summary>
    /// La plantilla del recibo de la orden.
    /// </summary>
    /// <param name="order">La orden ya pagada.</param>
    /// <returns>El nombre de la plantilla en ms-emails.</returns>
    public static string ReceiptTemplateOf(OrderAggregate order) => order.Type switch
    {
        OrderType.Purchase => PurchaseReceiptTemplate,
        OrderType.Package => PackageReceiptTemplate,
        _ => SubscriptionReceiptTemplate
    };

    /// <summary>
    /// Arma el diccionario que consume la plantilla del recibo de la orden (<see cref="ReceiptTemplateOf"/>).
    /// </summary>
    /// <param name="order">La orden ya pagada.</param>
    /// <param name="currency">La moneda de la orden, del catálogo de ms-locations: da el símbolo y los decimales.</param>
    /// <returns>Las variables de la plantilla, con los importes ya legibles («$ 773.500,00», <see cref="ReceiptText"/>).</returns>
    /// <remarks>
    /// Una orden de paquete no cobra la licencia: lleva solo las variables de <see cref="PackageReceiptTemplate"/>. Antes
    /// arrastraba las de la licencia con valores falsos («subtotal» $ 0, «IVA (0 %)», los módulos), y las de la licencia
    /// llevaban las del paquete vacías (pendings/739).
    /// </remarks>
    public static Dictionary<string, string> Build(OrderAggregate order, Currency currency)
    {
        var zone = SubscriptionTimeline.ZoneOf(order.TenantDetail);
        var variables = Common(order, zone, currency);

        if (order.Type == OrderType.Package)
        {
            // Una orden de paquete lleva una sola línea (OrderAggregate.CreatePackagePurchase).
            AddPackage(variables, order.Packages!.Single(), order.License.BillingType, zone, currency);

            return variables;
        }

        AddLicense(variables, order, zone, currency);

        return variables;
    }

    /// <summary>
    /// Las variables de toda plantilla de recibo: quién compra y para quién, la orden, su fecha, el crédito aplicado y lo
    /// pagado.
    /// </summary>
    private static Dictionary<string, string> Common(OrderAggregate order, DateTimeZone zone, Currency currency)
    {
        var purchasedAt = PurchasedAt(order).InZone(zone);
        var credit = order.Total - order.AmountToCharge;

        return new Dictionary<string, string>
        {
            ["organization_name"] = order.TenantDetail.Name,
            ["organization_email"] = order.TenantDetail.Email,
            ["organization_phone"] = order.TenantDetail.Phone,
            ["organization_document"] = $"{order.TenantDetail.TypeDocument.Code} {order.TenantDetail.NumberDocument}",
            ["buyer_name"] = order.Buyer.Name,
            ["buyer_email"] = order.Buyer.Email,
            ["credit_applied"] = ReceiptText.Amount(credit, currency),
            // Sin crédito la fila no se muestra: «Crédito aplicado -$ 0,00» en una renovación sin saldo a favor confundía.
            ["credit_row_display"] = RowDisplay(credit),
            ["amount_paid"] = ReceiptText.Amount(order.AmountToCharge, currency),
            ["purchase_date"] = DatePattern.Format(purchasedAt.Date),
            ["current_year"] = purchasedAt.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["order_id"] = order.Id.ToString()
        };
    }

    /// <summary>
    /// Las variables de los recibos de la licencia (<see cref="PurchaseReceiptTemplate"/>, <see cref="SubscriptionReceiptTemplate"/>
    /// y la confirmación de compra): la licencia con su impuesto, sus módulos y los paquetes que se compraron con ella.
    /// </summary>
    private static void AddLicense(Dictionary<string, string> variables, OrderAggregate order, DateTimeZone zone, Currency currency)
    {
        var packages = order.Packages ?? [];
        var packagesTotal = packages.Aggregate(Money.Zero(order.Total.Currency), (sum, x) => sum + x.Total);

        variables["license_name"] = order.License.Name;
        variables["billing_type"] = BillingTypeLabel(order.License.BillingType);
        variables["subtotal"] = ReceiptText.Amount(order.License.SubTotal, currency);
        variables["tax"] = ReceiptText.Amount(order.License.Tax, currency);
        variables["tax_label"] = TaxLabel(order.License.TaxTreatment, order.License.TaxBasisPoints);
        variables["tax_rate"] = TaxRate(order.License.TaxBasisPoints);
        variables["tax_legal_basis"] = order.License.TaxLegalBasis ?? string.Empty;
        variables["invoice_concept"] = order.License.InvoiceConcept ?? order.License.Name;
        // El precio de la orden entera: la licencia y sus paquetes (pendings/739).
        variables["total"] = ReceiptText.Amount(order.Total, currency);
        variables["price"] = ReceiptText.Amount(order.Total, currency);
        // SubscriptionReceipt la sigue pidiendo; desde PackageReceipt toda orden que la usa cobra la licencia.
        variables["license_row_display"] = "table-row";
        variables["packages_row_display"] = packages.Count == 0 ? "none" : "table-row";
        variables["packages_list"] = ReceiptText.Packages(order, currency);
        variables["packages_total"] = ReceiptText.Amount(packagesTotal, currency);
        variables["period"] = ReceiptText.Period(order, zone);
        variables["order_type"] = OrderTypeLabel(order.Type);
        // Los importes ya llevan el símbolo; el código sigue viajando para las plantillas guardadas antes de este cambio.
        variables["currency"] = order.License.Total.Currency;
        variables["modules_list"] = string.Join(", ", order.License.Modules.Select(m => m.Name));
    }

    /// <summary>
    /// Las variables del recibo de un paquete (<see cref="PackageReceiptTemplate"/>): su base, su impuesto y su total, y su
    /// vigencia con las fechas del calendario de la copropiedad (pendings/739).
    /// </summary>
    /// <remarks>
    /// El IVA depende del tratamiento del paquete: sin IVA su fila no se muestra, como la del crédito.
    /// </remarks>
    private static void AddPackage(Dictionary<string, string> variables, PackageLine package, BillingType billingType, DateTimeZone zone, Currency currency)
    {
        variables["package_name"] = package.Name;
        variables["package_concept"] = package.InvoiceConcept ?? package.Name;
        variables["package_subtotal"] = ReceiptText.Amount(package.SubTotal, currency);
        variables["package_tax"] = ReceiptText.Amount(package.Tax, currency);
        variables["package_tax_label"] = TaxLabel(package.TaxTreatment, package.TaxBasisPoints);
        variables["package_tax_row_display"] = RowDisplay(package.Tax);
        variables["package_total"] = ReceiptText.Amount(package.Total, currency);
        variables["package_period"] = package.PeriodStart is null || package.PeriodEnd is null
            ? string.Empty
            : $"{ReceiptText.Date(package.PeriodStart.Value.InZone(zone).Date)} — {ReceiptText.Date(package.PeriodEnd.Value.InZone(zone).Date)}";
        variables["package_term"] = PackageTermText(billingType);
    }

    /// <summary>
    /// Si se ve la fila de un importe: en cero no se muestra. La plantilla no tiene condicionales (solo reemplaza
    /// variables), así que el micro decide con el <c>display</c> de la fila.
    /// </summary>
    private static string RowDisplay(Money amount) => amount.Amount == 0 ? "none" : "table-row";

    /// <summary>
    /// Hasta cuándo dura el paquete según la periodicidad de la licencia, como lo decide <see cref="PackageTerm"/>: con
    /// licencia anual vence con su periodo y se renueva con ella; con mensual dura doce meses desde la compra.
    /// </summary>
    /// <remarks>
    /// No es un catálogo: son las dos periodicidades que <see cref="PackageTerm"/> sabe calcular, y una nueva exige código
    /// ahí antes que un texto aquí.
    /// </remarks>
    private static string PackageTermText(BillingType billingType) => billingType switch
    {
        BillingType.Annually => "Vence con el periodo de tu licencia anual y se renueva con ella. Funciona mientras la licencia esté al día.",
        BillingType.Monthly => "Dura doce meses desde la compra y no se renueva con tu licencia mensual. Funciona mientras la licencia esté al día.",
        _ => throw new ArgumentOutOfRangeException(nameof(billingType), billingType, "The billing type has no package term.")
    };

    /// <summary>
    /// La fila del impuesto en el recibo según el tratamiento del precio comprado (pendings/662): «IVA (19 %)» si es
    /// gravado, «Excluido de IVA» o «Exento de IVA» si no. Antes la plantilla decía «IVA (19%)» fijo.
    /// </summary>
    /// <remarks>
    /// El impuesto del precio de una licencia es el IVA: es el único que cobra la plataforma por la suscripción. No es un
    /// catálogo: son los tres tratamientos que <see cref="TaxTreatment"/> sabe validar, y uno nuevo exige código ahí antes
    /// que una etiqueta aquí.
    /// </remarks>
    private static string TaxLabel(TaxTreatment treatment, int basisPoints) => treatment switch
    {
        TaxTreatment.Taxed => $"IVA ({TaxRate(basisPoints)})",
        TaxTreatment.Excluded => "Excluido de IVA",
        TaxTreatment.Exempt => "Exento de IVA",
        _ => throw new ArgumentOutOfRangeException(nameof(treatment), treatment, "The tax treatment has no label.")
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
    /// No es un catálogo: son los tipos de orden que el micro sabe cobrar, y uno nuevo exige código antes que una
    /// etiqueta.
    /// </remarks>
    private static string OrderTypeLabel(OrderType type) => type switch
    {
        OrderType.Purchase => "Compra",
        OrderType.Renewal => "Renovación",
        OrderType.Change => "Cambio de licencia",
        OrderType.Package => "Paquete complementario",
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
