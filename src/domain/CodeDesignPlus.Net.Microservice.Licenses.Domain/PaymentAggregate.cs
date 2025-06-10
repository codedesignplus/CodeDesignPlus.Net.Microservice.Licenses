using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

public class PaymentAggregate(Guid id) : AggregateRoot(id)
{
    public Guid IdLicense { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; } = null!;
    public Buyer Buyer { get; private set; } = null!;
    public Tenant Organization { get; private set; } = null!;
    public string? Error { get; private set; }
    public bool IsSuccess { get; private set; }

    public PaymentAggregate(Guid id, Guid idLicense, PaymentMethod paymentMethod, Buyer buyer, Tenant organization, Guid tenant, bool isSuccess, string? error, bool isActive, Guid createdBy) : this(id)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdOrderIsRequired);
        DomainGuard.GuidIsEmpty(idLicense, Errors.LicenseIdIsRequired);
        DomainGuard.IsNull(paymentMethod, Errors.PaymentMethodIsRequired);
        DomainGuard.IsNull(buyer, Errors.BuyerIsRequired);
        DomainGuard.IsNull(organization, Errors.OrganizationIsRequired);

        IdLicense = idLicense;
        PaymentMethod = paymentMethod;
        Buyer = buyer;
        Organization = organization;
        IsSuccess = isSuccess;
        IsActive = isActive;
        Tenant = tenant;
        Error = error;

        CreatedAt = SystemClock.Instance.GetCurrentInstant();
        CreatedBy = createdBy;
    }

    public static PaymentAggregate Create(Guid id, Guid idLicense, PaymentMethod methodPay, Buyer buyer, Tenant organization, Guid tenant, bool isSuccess, string? error, bool isActive, Guid createdBy)
    {
        return new PaymentAggregate(id, idLicense, methodPay, buyer, organization, tenant, isSuccess, error, isActive, createdBy);
    }
}
