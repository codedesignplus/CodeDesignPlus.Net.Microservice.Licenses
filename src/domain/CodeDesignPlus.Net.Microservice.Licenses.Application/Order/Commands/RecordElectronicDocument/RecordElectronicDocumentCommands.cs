using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.RecordElectronicDocument;

/// <summary>
/// The tax authority validated an electronic document; if it is the invoice or a credit note of an order, the order keeps
/// its number, CUFE and files (pendings/661).
/// </summary>
/// <param name="DocumentId">The id of the document in ms-electronicinvoicing.</param>
/// <param name="Reference">The reference it was asked with.</param>
/// <param name="Prefix">The prefix of its number.</param>
/// <param name="Consecutive">The consecutive of its number.</param>
/// <param name="Cufe">Its CUFE.</param>
/// <param name="IssueDate">The issue date written on it.</param>
/// <param name="ValidatedAt">When it was validated.</param>
/// <param name="Xml">The signed XML in ms-filestorage.</param>
/// <param name="Pdf">The graphic representation in ms-filestorage.</param>
/// <param name="AttachedDocument">The attached document in ms-filestorage, when it came with the validation.</param>
public record RecordElectronicDocumentValidatedCommand(Guid DocumentId, string Reference, string Prefix, long Consecutive, string Cufe, LocalDate? IssueDate, Instant ValidatedAt, FileAttachment? Xml, FileAttachment? Pdf, FileAttachment? AttachedDocument = null) : IRequest;

/// <summary>
/// The provider or the tax authority rejected an electronic document; if it is of an order, the order keeps why and the
/// platform administrator is told (pendings/661, rule 55).
/// </summary>
/// <param name="DocumentId">The id of the document in ms-electronicinvoicing.</param>
/// <param name="Reference">The reference it was asked with.</param>
/// <param name="Prefix">The prefix of its number, when it had one.</param>
/// <param name="Consecutive">The consecutive of its number, when it had one.</param>
/// <param name="ByTaxAuthority">Whether the DIAN rejected it; otherwise the provider.</param>
/// <param name="Reasons">Why, as it came.</param>
/// <param name="RejectedAt">When it was rejected.</param>
/// <param name="EventId">The id of the event, so a redelivery does not notify twice.</param>
public record RecordElectronicDocumentRejectedCommand(Guid DocumentId, string Reference, string? Prefix, long? Consecutive, bool ByTaxAuthority, List<string> Reasons, Instant RejectedAt, Guid EventId) : IRequest;

/// <summary>
/// ms-electronicinvoicing kept, after validating it, the files of a document of an order (pendings/661, step 4).
/// </summary>
/// <param name="DocumentId">The id of the document.</param>
/// <param name="Reference">The reference it was asked with.</param>
/// <param name="Xml">The signed XML.</param>
/// <param name="AttachedDocument">The attached document, when it could be kept.</param>
/// <param name="Pdf">The graphic representation, when it could be kept.</param>
/// <param name="KeptAt">When they were kept.</param>
public record RecordElectronicDocumentFilesCommand(Guid DocumentId, string Reference, FileAttachment Xml, FileAttachment? AttachedDocument, FileAttachment? Pdf, Instant KeptAt) : IRequest;
