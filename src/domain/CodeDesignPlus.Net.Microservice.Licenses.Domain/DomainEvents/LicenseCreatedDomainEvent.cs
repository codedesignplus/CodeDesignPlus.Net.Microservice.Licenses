namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

[EventKey<LicenseAggregate>(1, "LicenseCreatedDomainEvent")]
public class LicenseCreatedDomainEvent(
     Guid aggregateId,
     Guid? eventId = null,
     Instant? occurredAt = null,
     Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    public static LicenseCreatedDomainEvent Create(Guid aggregateId)
    {
        return new LicenseCreatedDomainEvent(aggregateId);
    }
}
