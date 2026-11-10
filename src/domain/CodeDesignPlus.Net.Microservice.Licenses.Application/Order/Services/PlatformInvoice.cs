using System.Globalization;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using NodaTime.Text;
using EI = CodeDesignPlus.Net.gRpc.Clients.Services.ElectronicInvoicing;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// The electronic documents of an order paid to the platform, as ms-electronicinvoicing receives them (Kappali
/// pendings/661): the invoice of what the order paid and, in a license change or a package upgrade, the credit note on the
/// invoice of each order whose unused period it credits.
/// </summary>
/// <remarks>
/// <para>
/// The platform's own issuer issues them: no <c>issuer_tenant</c> and <c>platform_issuer = true</c>. The copropiedad is
/// the buyer, with the data the order took at checkout and, for what the order does not carry, the configuration in
/// <see cref="PlatformInvoicingOptions"/>. It goes without an address until the checkout asks for the municipality code
/// (pendings/661, step 1).
/// </para>
/// <para>
/// Amounts travel in minor units of the order's currency. The invoice says the full price of the order; what the credit
/// and the balance in favour paid goes as prepayments, so what is payable is what the gateway charged:
/// <c>payable = gross - discount + tax - prepayments</c>, the formula ms-electronicinvoicing checks.
/// </para>
/// <para>
/// The platform sells in cash (<see cref="PlatformInvoicingOptions.PaymentFormCode"/>), so no document carries a due date:
/// a cash sale is due on its issue date, which ms-electronicinvoicing puts when the provider receives it, after its retries
/// and maybe after midnight. Dating it here on the payment issued SETP991148967 on the 10th due on the 9th. A sale on
/// credit would have to send its due date: ms-electronicinvoicing refuses one without it.
/// </para>
/// <para>
/// Everything is computed from the order, so asking again builds the same content and ms-electronicinvoicing answers with
/// the same document instead of issuing a second one.
/// </para>
/// </remarks>
public static class PlatformInvoice
{
    /// <summary>
    /// The namespace of the ids of the invoices of the orders. A fixed value is what makes the id the same on every retry;
    /// it is not a setting, and changing it would invoice every order again.
    /// </summary>
    private static readonly Guid InvoiceNamespace = Guid.Parse("6f0d6a52-4c1e-4f8b-9a61-2e7c3d5b8a11");

    /// <summary>
    /// The namespace of the ids of the credit notes of the orders, apart from the invoices' so the two never collide.
    /// </summary>
    private static readonly Guid CreditNoteNamespace = Guid.Parse("b3e2c7d1-8a54-4e19-b6f0-7d4c2a9e5f13");

    /// <summary>
    /// The quantity of every line: a subscription is sold as one unit of its period.
    /// </summary>
    private const string OneUnit = "1";

    /// <summary>
    /// The id of the invoice of an order.
    /// </summary>
    /// <param name="orderId">The order.</param>
    /// <returns>The same id for the same order, on every retry.</returns>
    public static Guid InvoiceIdOf(Guid orderId) => DeterministicGuid.Create(InvoiceNamespace, orderId.ToString());

    /// <summary>
    /// The id of the credit note an order issues on the invoice of a previous order.
    /// </summary>
    /// <param name="orderId">The order that credits.</param>
    /// <param name="correctedOrderId">The order whose invoice is corrected.</param>
    /// <returns>The same id for the same pair, on every retry.</returns>
    public static Guid CreditNoteIdOf(Guid orderId, Guid correctedOrderId) => DeterministicGuid.Create(CreditNoteNamespace, $"{orderId}:{correctedOrderId}");

    /// <summary>
    /// The reference of the invoice of an order: it comes back in the events and is how the order is found again.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="orderId">The order.</param>
    /// <returns>The reference.</returns>
    public static string InvoiceReferenceOf(PlatformInvoicingOptions options, Guid orderId) => $"{options.ReferencePrefix}:{orderId}";

    /// <summary>
    /// The reference of a credit note of an order.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="orderId">The order that credits.</param>
    /// <param name="correctedOrderId">The order whose invoice is corrected.</param>
    /// <returns>The reference.</returns>
    public static string CreditNoteReferenceOf(PlatformInvoicingOptions options, Guid orderId, Guid correctedOrderId) => $"{options.ReferencePrefix}:{orderId}:credit-note:{correctedOrderId}";

    /// <summary>
    /// The order a reference of this service points to.
    /// </summary>
    /// <param name="options">The options.</param>
    /// <param name="reference">The reference that came in an event of ms-electronicinvoicing.</param>
    /// <returns>The order, or null when the reference is not of an order of this service (another caller's document).</returns>
    public static Guid? OrderIdOf(PlatformInvoicingOptions options, string? reference)
    {
        var prefix = options.ReferencePrefix + ":";

        if (string.IsNullOrWhiteSpace(reference) || string.IsNullOrWhiteSpace(options.ReferencePrefix) || !reference.StartsWith(prefix, StringComparison.Ordinal))
        {
            return null;
        }

        var rest = reference[prefix.Length..];
        var end = rest.IndexOf(':', StringComparison.Ordinal);

        return Guid.TryParse(end < 0 ? rest : rest[..end], out var orderId) ? orderId : null;
    }

    /// <summary>
    /// The tax authority's payment means of what an order paid.
    /// </summary>
    /// <remarks>
    /// An order the gateway charged nothing for (credit and balance paid it all) is a mutual agreement. A card goes by its
    /// type, credit or debit; a card whose type the gateway did not say, or an instrument the configuration does not know,
    /// is an instrument not defined. Nothing is guessed.
    /// </remarks>
    /// <param name="order">The paid order.</param>
    /// <param name="options">The options.</param>
    /// <returns>The code.</returns>
    public static string PaymentMeansCodeOf(OrderAggregate order, PlatformInvoicingOptions options)
    {
        if (order.AmountToCharge.Amount == 0)
        {
            return options.FullyPrepaidPaymentMeansCode;
        }

        var method = order.PaymentMethodType;

        if (string.IsNullOrWhiteSpace(method))
        {
            return options.UnknownPaymentMeansCode;
        }

        if (string.Equals(method, options.CardPaymentMethodType, StringComparison.OrdinalIgnoreCase))
        {
            return !string.IsNullOrWhiteSpace(order.CardType) && options.CardPaymentMeansCodes.TryGetValue(order.CardType, out var card) ? card : options.UnknownPaymentMeansCode;
        }

        return options.PaymentMeansCodes.TryGetValue(method, out var code) ? code : options.UnknownPaymentMeansCode;
    }

    /// <summary>
    /// The copropiedad as the buyer of the invoice.
    /// </summary>
    /// <param name="tenant">The copropiedad the order took at checkout.</param>
    /// <param name="options">The options.</param>
    /// <returns>The buyer, without an address.</returns>
    /// <exception cref="InvalidOperationException">Thrown when the configuration has no code for its document type.</exception>
    public static EI.Buyer BuyerOf(Tenant tenant, PlatformInvoicingOptions options)
    {
        ArgumentNullException.ThrowIfNull(tenant);

        var typeCode = tenant.TypeDocument?.Code ?? string.Empty;

        if (!options.IdentificationTypeCodes.TryGetValue(typeCode, out var identificationType))
        {
            throw new InvalidOperationException($"'{PlatformInvoicingOptions.Section}:{nameof(PlatformInvoicingOptions.IdentificationTypeCodes)}' has no code for the document type '{typeCode}' of tenant {tenant.Id}.");
        }

        var (number, checkDigit) = SplitCheckDigit(tenant.NumberDocument);

        var buyer = new EI.Buyer
        {
            PersonTypeCode = options.BuyerPersonTypeCode,
            IdentificationTypeCode = identificationType,
            IdentificationNumber = number,
            Name = tenant.Name,
            Email = tenant.Email,
            Phone = tenant.Phone,
            TaxLiabilityCode = options.BuyerTaxLiabilityCode
        };

        if (checkDigit is not null)
        {
            buyer.CheckDigit = checkDigit;
        }

        buyer.TaxResponsibilities.AddRange(options.BuyerTaxResponsibilities);

        return buyer;
    }

    /// <summary>
    /// The invoice of what a paid order sold: one line per license and package, the credit and the balance in favour as
    /// prepayments, and what the gateway charged as payable.
    /// </summary>
    /// <param name="order">The paid order.</param>
    /// <param name="correctedOrders">The orders whose unused period this one credits, to date their prepayments.</param>
    /// <param name="options">The options.</param>
    /// <param name="precededBy">The credit note that must be validated before the invoice is sent, if any.</param>
    /// <returns>The request.</returns>
    public static EI.IssueDocumentRequest Invoice(OrderAggregate order, IReadOnlyDictionary<Guid, OrderAggregate> correctedOrders, PlatformInvoicingOptions options, Guid? precededBy)
    {
        ArgumentNullException.ThrowIfNull(order);

        var paidAt = order.PaidAt ?? order.UpdatedAt ?? order.CreatedAt;
        var zone = ZoneOf(order.TenantDetail);
        var billing = order.ToBilling(paidAt);
        var currency = order.Total.Currency;
        var paidOn = paidAt.InZone(zone).Date;

        var content = new EI.DocumentContent
        {
            Buyer = BuyerOf(order.TenantDetail, options),
            // Without a due date: a cash sale is due on the day it is issued, and only ms-electronicinvoicing knows that day.
            PaymentMeans = new EI.PaymentMeans { FormCode = options.PaymentFormCode, MethodCode = PaymentMeansCodeOf(order, options) }
        };

        foreach (var line in billing.Lines)
        {
            content.Lines.Add(LineOf((line.PackageLineId ?? order.License.Id).ToString("N"), line.Concept, line.SubTotal.Amount, line.Tax.Amount, line.TaxTreatment, line.TaxBasisPoints, options));
        }

        content.Prepayments.AddRange(PrepaymentsOf(order, correctedOrders, zone, paidOn));
        content.Notes.Add($"Orden {order.Id}");
        content.Notes.Add($"Periodo del {Printed(billing.PeriodStart.InZone(zone).Date)} al {Printed(billing.PeriodEnd.InZone(zone).Date)}");
        content.Totals = TotalsOf(currency, content.Lines, content.Prepayments.Sum(x => x.Amount));

        var request = new EI.IssueDocumentRequest
        {
            Id = InvoiceIdOf(order.Id).ToString(),
            PlatformIssuer = true,
            Type = EI.DocumentType.Invoice,
            Reference = InvoiceReferenceOf(options, order.Id),
            Content = content
        };

        if (precededBy is { } preceding)
        {
            request.PrecededBy = preceding.ToString();
        }

        return request;
    }

    /// <summary>
    /// The credit note an order issues on the invoice of a previous order: the base and the VAT of the days that order
    /// paid and will not be served, because the license changed or the package grew.
    /// </summary>
    /// <param name="order">The order that credits.</param>
    /// <param name="corrected">The order whose invoice is corrected.</param>
    /// <param name="correctedInvoice">The validated invoice of that order.</param>
    /// <param name="options">The options.</param>
    /// <returns>The request.</returns>
    public static EI.IssueDocumentRequest CreditNote(OrderAggregate order, OrderAggregate corrected, OrderElectronicDocument correctedInvoice, PlatformInvoicingOptions options)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(corrected);
        ArgumentNullException.ThrowIfNull(correctedInvoice);

        var zone = ZoneOf(order.TenantDetail);
        var periods = CreditedPeriodsOf(order, corrected.Id);

        var content = new EI.DocumentContent
        {
            Buyer = BuyerOf(order.TenantDetail, options),
            PaymentMeans = new EI.PaymentMeans { FormCode = options.PaymentFormCode, MethodCode = PaymentMeansCodeOf(corrected, options) },
            BillingReference = new EI.BillingReference { DocumentId = correctedInvoice.DocumentId.ToString() },
            CorrectionConceptCode = options.CreditNoteCorrectionConceptCode
        };

        foreach (var period in periods)
        {
            var description = $"Devolución parcial: {period.UnusedDays} de {period.PeriodDays} días no prestados del periodo del {Printed(period.From.InZone(zone).Date)} al {Printed(period.To.InZone(zone).Date)}, por {ReasonOf(order)}";
            var treatment = period.TaxBasisPoints > 0 ? TaxTreatment.Taxed : TaxTreatment.Excluded;

            content.Lines.Add(LineOf((period.LineId ?? corrected.License.Id).ToString("N"), description, period.Amount.Amount, period.Tax?.Amount ?? 0, treatment, period.TaxBasisPoints, options));
        }

        content.Notes.Add($"Orden {order.Id}");
        content.Totals = TotalsOf(order.Total.Currency, content.Lines, 0);

        return new EI.IssueDocumentRequest
        {
            Id = CreditNoteIdOf(order.Id, corrected.Id).ToString(),
            PlatformIssuer = true,
            Type = EI.DocumentType.CreditNote,
            Reference = CreditNoteReferenceOf(options, order.Id, corrected.Id),
            Content = content
        };
    }

    /// <summary>
    /// The unused periods an order credits of a previous order, with something to credit.
    /// </summary>
    /// <param name="order">The order that credits.</param>
    /// <param name="correctedOrderId">The order whose periods are credited.</param>
    /// <returns>The periods; empty when it credits nothing of that order.</returns>
    public static List<CreditedPeriod> CreditedPeriodsOf(OrderAggregate order, Guid correctedOrderId) => [.. (order.CreditedPeriods ?? []).Where(x => x.OrderId == correctedOrderId && x.Total.Amount > 0)];

    /// <summary>
    /// The orders whose invoices an order corrects with a credit note: those with some unused period credited.
    /// </summary>
    /// <param name="order">The order that credits.</param>
    /// <returns>The ids, in the order they were credited.</returns>
    public static List<Guid> CorrectedOrderIdsOf(OrderAggregate order) => [.. (order.CreditedPeriods ?? []).Where(x => x.Total.Amount > 0).Select(x => x.OrderId).Distinct()];

    private static string ReasonOf(OrderAggregate order) => order.Type == OrderType.Package
        ? $"ampliación del paquete {order.Packages?.FirstOrDefault()?.Name}"
        : $"cambio al plan {order.License.Name}";

    /// <summary>
    /// What the credit and the balance in favour paid of the order, as prepayments that add up exactly to it: first the
    /// credit of each previous order (dated on its payment), then the balance in favour (dated on this payment).
    /// </summary>
    private static List<EI.Prepayment> PrepaymentsOf(OrderAggregate order, IReadOnlyDictionary<Guid, OrderAggregate> correctedOrders, DateTimeZone zone, LocalDate paidOn)
    {
        var prepayments = new List<EI.Prepayment>();
        var remaining = (order.Total - order.AmountToCharge).Amount;

        foreach (var group in (order.CreditedPeriods ?? []).GroupBy(x => x.OrderId))
        {
            var amount = Math.Min(remaining, group.Sum(x => x.Total.Amount));

            if (amount <= 0)
            {
                continue;
            }

            var receivedOn = correctedOrders.TryGetValue(group.Key, out var corrected) && corrected.PaidAt is { } paid ? paid.InZone(zone).Date : paidOn;

            prepayments.Add(new EI.Prepayment { Id = group.Key.ToString("N"), Amount = amount, ReceivedOn = DateOf(receivedOn) });
            remaining -= amount;
        }

        if (remaining > 0)
        {
            prepayments.Add(new EI.Prepayment { Id = $"saldo-{order.Id:N}", Amount = remaining, ReceivedOn = DateOf(paidOn) });
        }

        return prepayments;
    }

    private static EI.Line LineOf(string code, string description, long subTotal, long tax, TaxTreatment treatment, int taxBasisPoints, PlatformInvoicingOptions options)
    {
        var line = new EI.Line
        {
            Code = code,
            Description = description,
            Quantity = OneUnit,
            UnitCode = options.UnitCode,
            UnitPrice = subTotal,
            DiscountAmount = 0,
            Amount = subTotal
        };

        // An excluded price does not cause the VAT; an exempt one causes it at rate zero; a taxed one at its rate.
        if (treatment != TaxTreatment.Excluded)
        {
            line.Taxes.Add(new EI.LineTax { Code = options.VatTaxCode, Name = options.VatTaxName, RateBasisPoints = taxBasisPoints, BaseAmount = subTotal, Amount = tax });
        }

        return line;
    }

    private static EI.Totals TotalsOf(string currency, IEnumerable<EI.Line> lines, long prepayment)
    {
        var gross = lines.Sum(x => x.Amount);
        var tax = lines.Sum(x => x.Taxes.Sum(t => t.Amount));

        return new EI.Totals
        {
            Currency = currency,
            GrossAmount = gross,
            DiscountAmount = 0,
            TaxAmount = tax,
            WithholdingAmount = 0,
            PrepaymentAmount = prepayment,
            // The formula ms-electronicinvoicing checks: the withholdings do not lower what is payable.
            PayableAmount = gross + tax - prepayment
        };
    }

    /// <summary>
    /// Separates the check digit written after the dash (rule 70): «901888777-3» is the number 901888777 and the digit 3.
    /// The dots and spaces of a number written by hand are dropped.
    /// </summary>
    private static (string Number, string? CheckDigit) SplitCheckDigit(string document)
    {
        var clean = new string((document ?? string.Empty).Where(x => !char.IsWhiteSpace(x) && x != '.').ToArray());
        var dash = clean.LastIndexOf('-');

        if (dash > 0 && dash == clean.Length - 2 && char.IsDigit(clean[^1]))
        {
            return (clean[..dash], clean[^1..]);
        }

        return (clean, null);
    }

    private static DateTimeZone ZoneOf(Tenant tenant) => DateTimeZoneProviders.Tzdb.GetZoneOrNull(tenant.Location?.City?.Timezone ?? string.Empty)
        ?? DateTimeZoneProviders.Tzdb.GetZoneOrNull(tenant.Location?.Country?.Timezone ?? string.Empty)
        ?? throw new InvalidOperationException($"The tenant {tenant.Id} has no time zone in its city nor in its country: the dates of its invoice cannot be written.");

    private static string DateOf(LocalDate date) => LocalDatePattern.Iso.Format(date);

    private static string Printed(LocalDate date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
