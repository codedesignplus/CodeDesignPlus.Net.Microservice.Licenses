using CodeDesignPlus.Net.gRpc.Clients.Services.Payment;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.DomainEvents;

[EventKey("PaymentAggregate", 1, "PaymentResponseAssociatedDomainEvent", "ms-payments")]
public class PaymentResponseAssociatedDomainEvent(
    Guid aggregateId,
    string module,
    Guid ReferenceId,
    PaymentStatus status,
    Dictionary<string, string?> response,
    Guid? tenant,
    int failureReason = 0,
    string? providerMessage = null,
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

    /// <summary>
    /// Por qué no se aprobó, como número de <c>PaymentFailureReason</c> de ms-payments; cero si se aprobó o no lo supo.
    /// </summary>
    public int FailureReason { get; } = failureReason;

    /// <summary>
    /// Lo que dijo el banco o la pasarela del resultado, tal como lo dijo; nulo si no dijo nada.
    /// </summary>
    public string? ProviderMessage { get; } = providerMessage;
}
