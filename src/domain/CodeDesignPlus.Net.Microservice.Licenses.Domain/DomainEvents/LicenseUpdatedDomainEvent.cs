namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

[EventKey<LicenseAggregate>(1, "LicenseUpdatedDomainEvent")]
public class LicenseUpdatedDomainEvent(
     Guid aggregateId,
     Guid? eventId = null,
     Instant? occurredAt = null,
     Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    public static LicenseUpdatedDomainEvent Create(Guid aggregateId)
    {
        return new LicenseUpdatedDomainEvent(aggregateId);
    }
}
