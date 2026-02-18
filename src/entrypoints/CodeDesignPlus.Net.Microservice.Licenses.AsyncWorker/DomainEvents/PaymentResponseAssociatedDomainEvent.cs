using CodeDesignPlus.Net.gRpc.Clients.Services.Payment;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.DomainEvents;

[EventKey<OrderAggregate>(1, "PaymentResponseAssociatedDomainEvent", "ms-payments")]
public class PaymentResponseAssociatedDomainEvent(
    Guid aggregateId,
    string module,
    Guid ReferenceId,
    PaymentStatus status,
    Dictionary<string, string?> response,
    Guid? tenant,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{

    public string Module { get; } = module;
    public Guid ReferenceId { get; } = ReferenceId;
    public PaymentStatus Status { get; } = status;
    public Dictionary<string, string?> Response { get; } = response;
    public Guid? Tenant { get; } = tenant;
}
