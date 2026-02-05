using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

[EventKey<OrderAggregate>(1, "OrderPaidAndReadyForProvisioningDomainEvent")]
public class OrderPaidAndReadyForProvisioningDomainEvent(
    Guid aggregateId,
    Tenant tenantDetail,
    LicenseTenant license,
    Guid buyerId,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    public Tenant TenantDetail { get; } = tenantDetail;
    public LicenseTenant License { get; } = license;
    public Guid BuyerId { get; } = buyerId;

    public static OrderPaidAndReadyForProvisioningDomainEvent Create(Guid aggregateId, Tenant tenantDetail, LicenseTenant license, Guid buyerId)
    {
        return new OrderPaidAndReadyForProvisioningDomainEvent(aggregateId, tenantDetail, license, buyerId);
    }
}
