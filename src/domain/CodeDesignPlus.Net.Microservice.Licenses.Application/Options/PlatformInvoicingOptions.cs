using System.ComponentModel.DataAnnotations;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Options;

/// <summary>
/// How an order paid to the platform is invoiced through ms-electronicinvoicing (Kappali pendings/661): the codes of the
/// tax authority's lists that the order does not carry.
/// </summary>
/// <remarks>
/// <para>
/// Everything here is a code of a list of the tax authority, or a decision of the accountant, so it lives in the
/// configuration of the worker and not in the code. Its values for Colombia are in <c>appsettings.json</c>.
/// </para>
/// <para>
/// The buyer's person type and responsibilities are a default for a copropiedad (a legal person, not responsible for
/// VAT) until the checkout asks for them (pendings/661, step 1).
/// </para>
/// </remarks>
public class PlatformInvoicingOptions : IValidatableObject
{
    /// <summary>
    /// The configuration section.
    /// </summary>
    public static readonly string Section = "PlatformInvoicing";

    /// <summary>
    /// Whether the paid orders are invoiced. Off where there is no ms-electronicinvoicing.
    /// </summary>
    public bool Enable { get; set; }

    /// <summary>
    /// The reference that identifies the documents of an order in ms-electronicinvoicing: the prefix followed by the id of
    /// the order. It comes back in the events and is how the order is found again.
    /// </summary>
    public string ReferencePrefix { get; set; } = string.Empty;

    /// <summary>
    /// The buyer's person type in the tax authority's list (<c>1</c> legal person, <c>2</c> natural person).
    /// </summary>
    public string BuyerPersonTypeCode { get; set; } = string.Empty;

    /// <summary>
    /// The buyer's tax scheme in the tax authority's list (<c>ZZ</c> not applicable, <c>01</c> VAT).
    /// </summary>
    public string BuyerTaxLiabilityCode { get; set; } = string.Empty;

    /// <summary>
    /// The buyer's tax responsibilities in the tax authority's list (<c>R-99-PN</c> not applicable).
    /// </summary>
    public List<string> BuyerTaxResponsibilities { get; set; } = [];

    /// <summary>
    /// The tax authority's identification type by the code of the document type the tenant was registered with
    /// (<c>NIT</c> is <c>31</c>, <c>CC</c> is <c>13</c>…).
    /// </summary>
    public Dictionary<string, string> IdentificationTypeCodes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The payment form of the tax authority: the order was collected online before invoicing it, so it is cash (<c>1</c>).
    /// </summary>
    public string PaymentFormCode { get; set; } = string.Empty;

    /// <summary>
    /// The tax authority's payment means by the instrument the gateway reports (<c>PSE</c>, <c>NEQUI</c>…). A card is not
    /// here: it goes by <see cref="CardPaymentMeansCodes"/>.
    /// </summary>
    public Dictionary<string, string> PaymentMeansCodes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// How the gateway names a card payment (<c>CARD</c>): its code depends on whether it was credit or debit.
    /// </summary>
    public string CardPaymentMethodType { get; set; } = string.Empty;

    /// <summary>
    /// The tax authority's payment means of a card by its type as the gateway reports it (<c>CREDIT</c> is <c>48</c>,
    /// <c>DEBIT</c> is <c>49</c>).
    /// </summary>
    public Dictionary<string, string> CardPaymentMeansCodes { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// The payment means when the instrument is not known, or is a card whose type the gateway did not say (<c>1</c>,
    /// instrument not defined). Never cash: the money came in online.
    /// </summary>
    public string UnknownPaymentMeansCode { get; set; } = string.Empty;

    /// <summary>
    /// The payment means of an order paid in full with credit and balance in favour, where the gateway charged nothing
    /// (<c>ZZZ</c>, mutual agreement).
    /// </summary>
    public string FullyPrepaidPaymentMeansCode { get; set; } = string.Empty;

    /// <summary>
    /// The unit of measure of a line in the tax authority's list (<c>94</c>, unit).
    /// </summary>
    public string UnitCode { get; set; } = string.Empty;

    /// <summary>
    /// The code of the VAT in the tax authority's list of taxes (<c>01</c>).
    /// </summary>
    public string VatTaxCode { get; set; } = string.Empty;

    /// <summary>
    /// The name of the VAT as it is printed (<c>IVA</c>).
    /// </summary>
    public string VatTaxName { get; set; } = string.Empty;

    /// <summary>
    /// The correction concept of the credit note of a license change or a package upgrade in the tax authority's list
    /// (<c>1</c>, partial return or partial non-acceptance of the service), as the accountant decided.
    /// </summary>
    public string CreditNoteCorrectionConceptCode { get; set; } = string.Empty;

    /// <summary>
    /// How long the email of a paid order waits for its electronic invoice (pendings/661, step 4). After it, the email goes
    /// with the receipt alone and the invoice follows in its own email when it is validated.
    /// </summary>
    public TimeSpan EmailWaitForInvoice { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// The container of ms-filestorage where the platform's files live (its <c>FileScope:PlatformContainer</c>): the
    /// documents of the platform's issuer are read from there to attach them to the email.
    /// </summary>
    public Guid FilesContainer { get; set; }

    /// <inheritdoc/>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!Enable)
        {
            yield break;
        }

        var required = new Dictionary<string, string>
        {
            [nameof(ReferencePrefix)] = ReferencePrefix,
            [nameof(BuyerPersonTypeCode)] = BuyerPersonTypeCode,
            [nameof(BuyerTaxLiabilityCode)] = BuyerTaxLiabilityCode,
            [nameof(PaymentFormCode)] = PaymentFormCode,
            [nameof(CardPaymentMethodType)] = CardPaymentMethodType,
            [nameof(UnknownPaymentMeansCode)] = UnknownPaymentMeansCode,
            [nameof(FullyPrepaidPaymentMeansCode)] = FullyPrepaidPaymentMeansCode,
            [nameof(UnitCode)] = UnitCode,
            [nameof(VatTaxCode)] = VatTaxCode,
            [nameof(VatTaxName)] = VatTaxName,
            [nameof(CreditNoteCorrectionConceptCode)] = CreditNoteCorrectionConceptCode,
        };

        foreach (var (name, value) in required.Where(x => string.IsNullOrWhiteSpace(x.Value)))
        {
            yield return new ValidationResult($"'{Section}:{name}' is required when the platform invoicing is enabled.", [name]);
        }

        if (BuyerTaxResponsibilities is not { Count: > 0 })
        {
            yield return new ValidationResult($"'{Section}:{nameof(BuyerTaxResponsibilities)}' needs at least one responsibility.", [nameof(BuyerTaxResponsibilities)]);
        }

        if (EmailWaitForInvoice <= TimeSpan.Zero)
        {
            yield return new ValidationResult($"'{Section}:{nameof(EmailWaitForInvoice)}' must be greater than zero.", [nameof(EmailWaitForInvoice)]);
        }

        if (FilesContainer == Guid.Empty)
        {
            yield return new ValidationResult($"'{Section}:{nameof(FilesContainer)}' is required: the files of the invoices are read from it.", [nameof(FilesContainer)]);
        }

        if (IdentificationTypeCodes is not { Count: > 0 })
        {
            yield return new ValidationResult($"'{Section}:{nameof(IdentificationTypeCodes)}' needs the code of each document type.", [nameof(IdentificationTypeCodes)]);
        }
    }
}
