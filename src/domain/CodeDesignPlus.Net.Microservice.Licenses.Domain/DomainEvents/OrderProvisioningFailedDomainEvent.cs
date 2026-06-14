namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

[EventKey<OrderAggregate>(1, "OrderProvisioningFailedDomainEvent")]
public class OrderProvisioningFailedDomainEvent(
    Guid aggregateId,
    string stepName,
    string error,
    Guid buyerId,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    public string StepName { get; } = stepName;
    public string Error { get; } = error;
    public Guid BuyerId { get; } = buyerId;

    public static OrderProvisioningFailedDomainEvent Create(Guid aggregateId, string stepName, string error, Guid buyerId)
        => new(aggregateId, stepName, error, buyerId);
}
