using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

[EventKey("EmailAggregate", 1, "SendEmailDomainEvent", "ms-emails")]
public class SendEmailDomainEvent(
    Guid aggregateId,
    string templateName,
    List<string> to,
    List<string> cc,
    List<string> bcc,
    Dictionary<string, string> variables,
    List<FileAttachment> attachments,
    Guid tenant,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    public string TemplateName { get; } = templateName;
    public List<string> To { get; } = to;
    public List<string> Cc { get; } = cc;
    public List<string> Bcc { get; } = bcc;
    public Dictionary<string, string> Variables { get; } = variables;
    public List<FileAttachment> Attachments { get; } = attachments;
    public Guid Tenant { get; } = tenant;
}
