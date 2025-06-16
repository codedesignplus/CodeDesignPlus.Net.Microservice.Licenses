using System.Security.Cryptography.X509Certificates;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

public class OrderAggregate(Guid id) : AggregateRootBase(id)
{
    public Guid IdLicense { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; } = null!;
    public Buyer Buyer { get; private set; } = null!;
    public Tenant TenantDetail { get; private set; } = null!;
    public string? Error { get; private set; }
    public bool IsSuccess { get; private set; }    
    public PaymentResponse PaymentResponse { get; private set; } = null!;

    public OrderAggregate(Guid id, Guid idLicense, PaymentMethod paymentMethod, Buyer buyer, Tenant tenantDetail, Guid createdBy) : this(id)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdOrderIsRequired);
        DomainGuard.GuidIsEmpty(idLicense, Errors.LicenseIdIsRequired);
        DomainGuard.IsNull(paymentMethod, Errors.PaymentMethodIsRequired);
        DomainGuard.IsNull(buyer, Errors.BuyerIsRequired);
        DomainGuard.IsNull(tenantDetail, Errors.TenantDetailIsRequired);

        IdLicense = idLicense;
        PaymentMethod = paymentMethod;
        Buyer = buyer;
        TenantDetail = tenantDetail;
        IsActive = true;

        CreatedAt = SystemClock.Instance.GetCurrentInstant();
        CreatedBy = createdBy;
    }

    public static OrderAggregate Create(Guid id, Guid idLicense, PaymentMethod methodPay, Buyer buyer, Tenant tenantDetail, Guid createdBy)
    {
        return new OrderAggregate(id, idLicense, methodPay, buyer, tenantDetail, createdBy);
    }

    public void SetPaymentResponse(PaymentResponse response)
    {
        DomainGuard.IsNull(response, Errors.PaymentResponseIsRequired);

        PaymentResponse = response;
    }
}
