namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// What the platform administrator reads when an electronic document of an order needs a person (Kappali pendings/661):
/// the invoice or the credit note was rejected, or the credit note could not be issued.
/// </summary>
/// <remarks>
/// The notices are in Spanish, as the rest of the notices of this service: the bell does not translate them.
/// </remarks>
public static class ElectronicInvoiceText
{
    /// <summary>
    /// The title of a rejected document.
    /// </summary>
    public const string RejectedTitle = "Factura electrónica rechazada";

    /// <summary>
    /// The title of a credit note that was not sent.
    /// </summary>
    public const string ReviewTitle = "Nota crédito pendiente de revisión";

    /// <summary>
    /// The text of a rejected document.
    /// </summary>
    /// <param name="tenantName">The copropiedad that bought.</param>
    /// <param name="isCreditNote">Whether it is a credit note and not the invoice.</param>
    /// <param name="byTaxAuthority">Whether the DIAN rejected it; otherwise the provider.</param>
    /// <param name="reasons">Why, as it came.</param>
    /// <returns>The text.</returns>
    public static string Rejected(string tenantName, bool isCreditNote, bool byTaxAuthority, IEnumerable<string> reasons)
    {
        var document = isCreditNote ? "la nota crédito" : "la factura electrónica";
        var who = byTaxAuthority ? "La DIAN" : "El proveedor de facturación";
        var why = string.Join("; ", (reasons ?? []).Where(x => !string.IsNullOrWhiteSpace(x)));

        return string.IsNullOrEmpty(why)
            ? $"{who} rechazó {document} de la orden de {tenantName}. Corrígela y vuelve a enviarla."
            : $"{who} rechazó {document} de la orden de {tenantName}: {why}. Corrígela y vuelve a enviarla.";
    }

    /// <summary>
    /// Why a credit note was not sent: the invoice it corrects is not in the system.
    /// </summary>
    /// <param name="correctedOrderId">The order whose invoice it corrects.</param>
    /// <returns>The reason.</returns>
    public static string WithoutInvoice(Guid correctedOrderId) =>
        $"La orden {correctedOrderId} no tiene factura electrónica en el sistema (se facturó a mano o antes de la facturación electrónica): emite la nota crédito a mano sobre esa factura.";

    /// <summary>
    /// Why a credit note was not sent: the invoice it corrects was rejected.
    /// </summary>
    /// <param name="correctedOrderId">The order whose invoice it corrects.</param>
    /// <returns>The reason.</returns>
    public static string InvoiceRejected(Guid correctedOrderId) =>
        $"La factura de la orden {correctedOrderId} está rechazada: corrígela antes de emitir la nota crédito.";

    /// <summary>
    /// The text of a credit note that was not sent.
    /// </summary>
    /// <param name="tenantName">The copropiedad.</param>
    /// <param name="reason">Why it was not sent.</param>
    /// <returns>The text.</returns>
    public static string Review(string tenantName, string reason) => $"La orden de {tenantName} acredita días no prestados, pero la nota crédito no se emitió. {reason}";

    /// <summary>
    /// The template of ms-emails of the email that carries only the invoice, when it arrives after the receipt
    /// (pendings/661, step 4).
    /// </summary>
    public const string InvoiceTemplate = "SubscriptionInvoiceIssued";

    /// <summary>
    /// The variable of the templates of the purchase that says the invoice is attached; empty when it is not.
    /// </summary>
    public const string NoteVariable = "electronic_invoice_note";

    /// <summary>
    /// What the email says when the invoice goes attached.
    /// </summary>
    /// <param name="number">The number of the invoice, its prefix followed by its consecutive.</param>
    /// <returns>The sentence.</returns>
    public static string AttachedNote(string number) =>
        $"También va adjunta la factura electrónica de venta {number}: su XML y un archivo ZIP con el documento validado por la DIAN y su representación gráfica en PDF.";

    /// <summary>
    /// The name of the XML of the invoice attached to the email.
    /// </summary>
    /// <param name="number">The number of the invoice.</param>
    /// <returns>The name.</returns>
    /// <remarks>
    /// The tax authority's names for the files delivered to the buyer (<c>z</c> and <c>ad</c> followed by the NIT, the code
    /// of the provider, the year and a hexadecimal consecutive of the issuer) need data the providers do not give; the
    /// number of the invoice names them unambiguously.
    /// </remarks>
    public static string XmlFileName(string number) => $"{number}.xml";

    /// <summary>
    /// The name of the ZIP with the attached document and the graphic representation.
    /// </summary>
    /// <param name="number">The number of the invoice.</param>
    /// <returns>The name.</returns>
    public static string ZipFileName(string number) => $"{number}.zip";

    /// <summary>
    /// The name of the attached document inside the ZIP: <c>ad</c>, as the tax authority prefixes it, and the number.
    /// </summary>
    /// <param name="number">The number of the invoice.</param>
    /// <returns>The name.</returns>
    public static string AttachedDocumentEntry(string number) => $"ad{number}.xml";

    /// <summary>
    /// The name of the graphic representation inside the ZIP.
    /// </summary>
    /// <param name="number">The number of the invoice.</param>
    /// <returns>The name.</returns>
    public static string PdfEntry(string number) => $"{number}.pdf";
}
