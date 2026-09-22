namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.DomainEvents;

[EventKey("UserAggregate", 1, "UserProvisioningFailedForOrderDomainEvent", "ms-users")]
public class UserProvisioningFailedForOrderDomainEvent(
    Guid aggregateId,
    Guid orderId,
    string reason,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    public Guid OrderId { get; } = orderId;
    public string Reason { get; } = reason;
}
