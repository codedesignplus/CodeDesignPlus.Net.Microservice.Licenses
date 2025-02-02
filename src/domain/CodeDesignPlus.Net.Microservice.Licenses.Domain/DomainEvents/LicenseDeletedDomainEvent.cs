namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

[EventKey<LicenseAggregate>(1, "LicenseDeletedDomainEvent")]
public class LicenseDeletedDomainEvent(
     Guid aggregateId,
     Guid? eventId = null,
     Instant? occurredAt = null,
     Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    public static LicenseDeletedDomainEvent Create(Guid aggregateId)
    {
        return new LicenseDeletedDomainEvent(aggregateId);
    }
}
