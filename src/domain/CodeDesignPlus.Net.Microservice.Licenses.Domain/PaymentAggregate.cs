using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

public class PaymentAggregate(Guid id) : AggregateRootBase(id)
{
    public MethodPay MethodPay { get; private set; } = default!;
    public Organization Organization { get; private set; } = default!;
    public Buyer Buyer { get; private set; } = default!;
    
    public static PaymentAggregate Create(Guid id, Guid tenant, Guid createBy)
    {
        return default;
    }
}
