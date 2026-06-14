namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.DomainEvents;

[EventKey("UserAggregate", 1, "UserProvisionedForOrderDomainEvent", "ms-users")]
public class UserProvisionedForOrderDomainEvent(
    Guid aggregateId,
    Guid orderId,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    public Guid OrderId { get; } = orderId;
}
