using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// An electronic document of an order as ms-electronicinvoicing reported it: the invoice of what the order paid, or a
/// credit note on the invoice of a previous order (pendings/661).
/// </summary>
/// <remarks>
/// The id is derived from the order (and, in a note, from the order it corrects), so asking again issues nothing new. The
/// order is found again from the events of ms-electronicinvoicing by that id or by <see cref="Reference"/>, never by their
/// tenant, which is the platform's reserved one.
/// </remarks>
/// <param name="DocumentId">The id of the document in ms-electronicinvoicing.</param>
/// <param name="Reference">The reference sent with it, which comes back in its events.</param>
/// <param name="CorrectedOrderId">In a credit note, the order whose invoice it corrects; null in an invoice.</param>
/// <param name="State">Where it stands.</param>
/// <param name="Prefix">The prefix of its number, once it has one.</param>
/// <param name="Consecutive">The consecutive of its number, once it has one.</param>
/// <param name="Cufe">The CUFE (CUDE in a note), once validated.</param>
/// <param name="IssueDate">The issue date written on the document, once validated.</param>
/// <param name="ValidatedAt">When the tax authority validated it.</param>
/// <param name="Xml">The signed XML in ms-filestorage, once validated.</param>
/// <param name="Pdf">The graphic representation in ms-filestorage, once validated.</param>
/// <param name="Reasons">Why it was rejected or why it needs review; empty otherwise.</param>
/// <param name="UpdatedAt">When it last changed.</param>
public sealed record OrderElectronicDocument(Guid DocumentId, string Reference, Guid? CorrectedOrderId, ElectronicDocumentState State, string? Prefix, long? Consecutive, string? Cufe, LocalDate? IssueDate, Instant? ValidatedAt, FileAttachment? Xml, FileAttachment? Pdf, List<string> Reasons, Instant UpdatedAt)
{
    /// <summary>
    /// A document just asked of ms-electronicinvoicing.
    /// </summary>
    /// <param name="documentId">The id derived from the order.</param>
    /// <param name="reference">The reference that comes back in its events.</param>
    /// <param name="correctedOrderId">In a credit note, the order whose invoice it corrects.</param>
    /// <param name="now">When it is asked.</param>
    /// <returns>The document, requested.</returns>
    public static OrderElectronicDocument Requested(Guid documentId, string reference, Guid? correctedOrderId, Instant now)
    {
        DomainGuard.GuidIsEmpty(documentId, Errors.ElectronicDocumentIdIsRequired);

        return new OrderElectronicDocument(documentId, reference, correctedOrderId, ElectronicDocumentState.Requested, null, null, null, null, null, null, null, [], now);
    }

    /// <summary>
    /// A credit note that is not sent because a person has to look at it first.
    /// </summary>
    /// <param name="documentId">The id derived from the order and the order it corrects.</param>
    /// <param name="reference">The reference it would have carried.</param>
    /// <param name="correctedOrderId">The order whose invoice it would correct.</param>
    /// <param name="reason">Why it was not sent.</param>
    /// <param name="now">When it was decided.</param>
    /// <returns>The note, waiting for review.</returns>
    public static OrderElectronicDocument ForReview(Guid documentId, string reference, Guid correctedOrderId, string reason, Instant now)
    {
        DomainGuard.GuidIsEmpty(documentId, Errors.ElectronicDocumentIdIsRequired);

        return new OrderElectronicDocument(documentId, reference, correctedOrderId, ElectronicDocumentState.NeedsReview, null, null, null, null, null, null, null, [reason], now);
    }

    /// <summary>
    /// The document validated by the tax authority.
    /// </summary>
    /// <param name="prefix">The prefix of its number.</param>
    /// <param name="consecutive">The consecutive of its number.</param>
    /// <param name="cufe">Its CUFE.</param>
    /// <param name="issueDate">The issue date written on it.</param>
    /// <param name="validatedAt">When it was validated.</param>
    /// <param name="xml">The signed XML, when it could be stored.</param>
    /// <param name="pdf">The graphic representation, when it could be stored.</param>
    /// <returns>The document, validated.</returns>
    public OrderElectronicDocument Validate(string prefix, long consecutive, string cufe, LocalDate? issueDate, Instant validatedAt, FileAttachment? xml, FileAttachment? pdf) =>
        this with { State = ElectronicDocumentState.Validated, Prefix = prefix, Consecutive = consecutive, Cufe = cufe, IssueDate = issueDate, ValidatedAt = validatedAt, Xml = xml, Pdf = pdf, Reasons = [], UpdatedAt = validatedAt };

    /// <summary>
    /// The document rejected by the provider or the tax authority.
    /// </summary>
    /// <param name="prefix">The prefix of its number, when it had one.</param>
    /// <param name="consecutive">The consecutive of its number, when it had one.</param>
    /// <param name="reasons">Why, as it came.</param>
    /// <param name="rejectedAt">When it was rejected.</param>
    /// <returns>The document, rejected.</returns>
    public OrderElectronicDocument Reject(string? prefix, long? consecutive, List<string> reasons, Instant rejectedAt) =>
        this with { State = ElectronicDocumentState.Rejected, Prefix = prefix ?? Prefix, Consecutive = consecutive ?? Consecutive, Reasons = [.. reasons ?? []], UpdatedAt = rejectedAt };

    /// <summary>
    /// The number as it is printed, once it has one: the prefix followed by the consecutive.
    /// </summary>
    public string? Number => Consecutive is null ? null : string.Concat(Prefix, Consecutive.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
