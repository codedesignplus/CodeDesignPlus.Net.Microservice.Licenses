using StoredFile = CodeDesignPlus.Net.ValueObjects.FileServer.File;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.DomainEvents;

/// <summary>
/// ms-electronicinvoicing: the tax authority validated an electronic document (pendings/661). Only what this service reads;
/// the rest of the event is ignored.
/// </summary>
/// <remarks>
/// The documents of the platform's issuer carry its reserved tenant (<c>00000000-e1ec-0000-0000-000000000001</c>), not the
/// copropiedad's: the order is found by <see cref="Reference"/> and the document by its id.
/// </remarks>
[EventKey("ElectronicDocumentAggregate", 1, "ElectronicDocumentValidatedDomainEvent", "ms-electronicinvoicing")]
public class ElectronicDocumentValidatedDomainEvent(
    Guid aggregateId,
    Guid tenant,
    string reference,
    string prefix,
    long consecutive,
    string cufe,
    LocalDate? issueDate,
    Instant validatedAt,
    StoredFile? xml,
    StoredFile? pdf,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    /// <summary>
    /// The tenant of the issuer; for the platform's documents, its reserved id.
    /// </summary>
    public Guid Tenant { get; } = tenant;

    /// <summary>
    /// The reference the document was asked with.
    /// </summary>
    public string Reference { get; } = reference;

    /// <summary>
    /// The prefix of its number.
    /// </summary>
    public string Prefix { get; } = prefix;

    /// <summary>
    /// The consecutive of its number.
    /// </summary>
    public long Consecutive { get; } = consecutive;

    /// <summary>
    /// The CUFE (CUDE in a note).
    /// </summary>
    public string Cufe { get; } = cufe;

    /// <summary>
    /// The issue date written on the document.
    /// </summary>
    public LocalDate? IssueDate { get; } = issueDate;

    /// <summary>
    /// When it was validated.
    /// </summary>
    public Instant ValidatedAt { get; } = validatedAt;

    /// <summary>
    /// The signed XML in ms-filestorage, when it could be stored.
    /// </summary>
    public StoredFile? Xml { get; } = xml;

    /// <summary>
    /// The graphic representation in ms-filestorage, when it could be stored.
    /// </summary>
    public StoredFile? Pdf { get; } = pdf;

    /// <summary>
    /// The attached document the tax authority answered with, when it could be stored; absent in the events of a version
    /// of ms-electronicinvoicing that did not publish it.
    /// </summary>
    public StoredFile? AttachedDocument { get; init; }
}

/// <summary>
/// ms-electronicinvoicing: the files of a document validated without them were kept afterwards (pendings/661, step 4). Only
/// what this service reads.
/// </summary>
[EventKey("ElectronicDocumentAggregate", 1, "ElectronicDocumentFilesKeptDomainEvent", "ms-electronicinvoicing")]
public class ElectronicDocumentFilesKeptDomainEvent(
    Guid aggregateId,
    Guid tenant,
    string reference,
    StoredFile xml,
    StoredFile? attachedDocument,
    StoredFile? pdf,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    /// <summary>
    /// The tenant of the issuer; for the platform's documents, its reserved id.
    /// </summary>
    public Guid Tenant { get; } = tenant;

    /// <summary>
    /// The reference the document was asked with.
    /// </summary>
    public string Reference { get; } = reference;

    /// <summary>
    /// The signed XML in ms-filestorage.
    /// </summary>
    public StoredFile Xml { get; } = xml;

    /// <summary>
    /// The attached document in ms-filestorage, when it could be kept.
    /// </summary>
    public StoredFile? AttachedDocument { get; } = attachedDocument;

    /// <summary>
    /// The graphic representation in ms-filestorage, when it could be kept.
    /// </summary>
    public StoredFile? Pdf { get; } = pdf;
}

/// <summary>
/// Who rejected an electronic document and why, as ms-electronicinvoicing publishes it.
/// </summary>
/// <param name="Origin">1 the provider, 2 the tax authority.</param>
/// <param name="Reasons">Why, as it came.</param>
/// <param name="At">When.</param>
public sealed record ElectronicDocumentRejection(int Origin, List<string> Reasons, Instant At)
{
    /// <summary>
    /// The origin of a rejection by the tax authority in ms-electronicinvoicing.
    /// </summary>
    public const int TaxAuthority = 2;
}

/// <summary>
/// ms-electronicinvoicing: the provider or the tax authority rejected an electronic document (pendings/661). Only what this
/// service reads.
/// </summary>
[EventKey("ElectronicDocumentAggregate", 1, "ElectronicDocumentRejectedDomainEvent", "ms-electronicinvoicing")]
public class ElectronicDocumentRejectedDomainEvent(
    Guid aggregateId,
    Guid tenant,
    string reference,
    string? prefix,
    long? consecutive,
    ElectronicDocumentRejection rejection,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    /// <summary>
    /// The tenant of the issuer; for the platform's documents, its reserved id.
    /// </summary>
    public Guid Tenant { get; } = tenant;

    /// <summary>
    /// The reference the document was asked with.
    /// </summary>
    public string Reference { get; } = reference;

    /// <summary>
    /// The prefix of its number, when it had one.
    /// </summary>
    public string? Prefix { get; } = prefix;

    /// <summary>
    /// The consecutive of its number, when it had one.
    /// </summary>
    public long? Consecutive { get; } = consecutive;

    /// <summary>
    /// Who rejected it and why.
    /// </summary>
    public ElectronicDocumentRejection Rejection { get; } = rejection;
}
