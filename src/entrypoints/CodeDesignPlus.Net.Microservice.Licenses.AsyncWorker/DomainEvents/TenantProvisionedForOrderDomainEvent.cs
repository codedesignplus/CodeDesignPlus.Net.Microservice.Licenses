namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.DomainEvents;

[EventKey("TenantAggregate", 1, "TenantProvisionedForOrderDomainEvent", "ms-tenants")]
public class TenantProvisionedForOrderDomainEvent(
    Guid aggregateId,
    Guid orderId,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    public Guid OrderId { get; } = orderId;
}
