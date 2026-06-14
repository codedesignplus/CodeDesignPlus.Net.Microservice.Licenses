namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

[EventKey<OrderAggregate>(1, "OrderProvisioningCompletedDomainEvent")]
public class OrderProvisioningCompletedDomainEvent(
    Guid aggregateId,
    Guid buyerId,
    Guid tenantId,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    public Guid BuyerId { get; } = buyerId;
    public Guid TenantId { get; } = tenantId;

    public static OrderProvisioningCompletedDomainEvent Create(Guid aggregateId, Guid buyerId, Guid tenantId)
        => new(aggregateId, buyerId, tenantId);
}
