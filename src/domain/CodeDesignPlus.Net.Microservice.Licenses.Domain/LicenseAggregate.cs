namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

public class LicenseAggregate(Guid id) : AggregateRoot(id)
{
    public static LicenseAggregate Create(Guid id, Guid tenant, Guid createBy)
    {
       return default;
    }
}
