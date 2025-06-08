using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

public class PaymentAggregate(Guid id) : AggregateRoot(id)
{
    public MethodPay MethodPay { get; private set; } = null!;
    public Buyer Buyer { get; private set; } = null!;
    public Organization Organization { get; private set; } = null!;
    public string? Error { get; private set; }
    public bool IsSuccess { get; private set; }

    public PaymentAggregate(Guid id, MethodPay methodPay, Buyer buyer, Organization organization, Guid tenant, bool isSuccess, string? error, bool isActive, Guid createdBy) : this(id)
    {
        DomainGuard.IsNull(methodPay, Errors.MethodPayIsRequired);
        DomainGuard.IsNull(buyer, Errors.BuyerIsRequired);
        DomainGuard.IsNull(organization, Errors.OrganizationIsRequired);

        MethodPay = methodPay;
        Buyer = buyer;
        Organization = organization;
        IsSuccess = isSuccess;
        IsActive = isActive;
        Tenant = tenant;
        Error = error;

        CreatedAt = SystemClock.Instance.GetCurrentInstant();
        CreatedBy = createdBy;
    }

    public static PaymentAggregate Create(Guid id, MethodPay methodPay, Buyer buyer, Organization organization, Guid tenant, bool isSuccess, string? error, bool isActive, Guid createdBy)
    {
        return new PaymentAggregate(id, methodPay, buyer, organization, tenant, isSuccess, error, isActive, createdBy);
    }
}
