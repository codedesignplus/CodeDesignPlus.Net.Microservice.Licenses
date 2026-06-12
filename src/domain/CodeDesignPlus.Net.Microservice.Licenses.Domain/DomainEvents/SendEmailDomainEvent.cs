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
    public string TemplateName { get; private set; } = templateName;
    public List<string> To { get; private set; } = to;
    public List<string> Cc { get; private set; } = cc;
    public List<string> Bcc { get; private set; } = bcc;
    public Dictionary<string, string> Variables { get; private set; } = variables;
    public List<FileAttachment> Attachments { get; private set; } = attachments;
    public Guid Tenant { get; private set; } = tenant;
}
