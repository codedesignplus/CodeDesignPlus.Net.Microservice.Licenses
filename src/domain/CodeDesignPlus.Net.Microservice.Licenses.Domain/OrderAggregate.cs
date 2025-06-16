using System.Security.Cryptography.X509Certificates;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

public class OrderAggregate(Guid id) : AggregateRoot(id)
{
    public Guid IdLicense { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; } = null!;
    public Buyer Buyer { get; private set; } = null!;
    public Tenant Organization { get; private set; } = null!;
    public string? Error { get; private set; }
    public bool IsSuccess { get; private set; }    
    public PaymentResponse Response { get; private set; } = null!;

    public OrderAggregate(Guid id, Guid idLicense, PaymentMethod paymentMethod, Buyer buyer, Tenant organization, Guid tenant, bool isActive, Guid createdBy) : this(id)
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
        IsActive = isActive;
        Tenant = tenant;

        CreatedAt = SystemClock.Instance.GetCurrentInstant();
        CreatedBy = createdBy;
    }

    public static OrderAggregate Create(Guid id, Guid idLicense, PaymentMethod methodPay, Buyer buyer, Tenant organization, Guid tenant, bool isActive, Guid createdBy)
    {
        return new OrderAggregate(id, idLicense, methodPay, buyer, organization, tenant, isActive, createdBy);
    }

    public void SetPaymentResponse(PaymentResponse response)
    {
        DomainGuard.IsNull(response, Errors.PaymentResponseIsRequired);

        Response = response;
    }
}
