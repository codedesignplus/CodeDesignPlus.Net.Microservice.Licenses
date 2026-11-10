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
}
