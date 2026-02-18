using System.Security.Cryptography.X509Certificates;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

public class OrderAggregate(Guid id) : AggregateRootBase(id)
{
    public Guid PaymentId { get; private set; }
    public License License { get; private set; } = null!;
    public PaymentMethod PaymentMethod { get; private set; } = null!;
    public Buyer Buyer { get; private set; } = null!;
    public Tenant TenantDetail { get; private set; } = null!;
    public string? Error { get; private set; }
    public bool IsSuccess { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }

    public static OrderAggregate Create(Guid id, Guid paymentId, License license, PaymentMethod paymentMethod, Buyer buyer, Tenant tenantDetail, Guid createdBy)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdOrderIsRequired);
        DomainGuard.GuidIsEmpty(paymentId, Errors.IdPaymentIsRequired);
        DomainGuard.IsNull(license, Errors.LicenseIdIsRequired);
        DomainGuard.IsNull(paymentMethod, Errors.PaymentMethodIsRequired);
        DomainGuard.IsNull(buyer, Errors.BuyerIsRequired);
        DomainGuard.IsNull(tenantDetail, Errors.TenantDetailIsRequired);

        var aggregate = new OrderAggregate(id)
        {
            PaymentId = paymentId,
            License = license,
            PaymentMethod = paymentMethod,
            Buyer = buyer,
            PaymentStatus = PaymentStatus.Initiated,
            TenantDetail = tenantDetail,
            IsActive = true,

            CreatedAt = SystemClock.Instance.GetCurrentInstant(),
            CreatedBy = createdBy
        };

        return aggregate;
    }

    public void SetPaymentStatus(PaymentStatus paymentStatus, Dictionary<string, string> metadata, Guid buyerId)
    {
        PaymentStatus = paymentStatus;

        if (paymentStatus == PaymentStatus.Succeeded)
        {
            var @event = OrderPaidAndReadyForProvisioningDomainEvent.Create(
                this.Id, 
                TenantDetail,
                LicenseTenant.Create(
                    License.Id,
                    License.Name,
                    SystemClock.Instance.GetCurrentInstant(),
                    SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(License.BillingType == BillingTypeEnum.Monthly ? 30 : 365)),
                    metadata
                ),
                buyerId
            );

            AddEvent(@event);
        }

        UpdatedAt = SystemClock.Instance.GetCurrentInstant();
    }
}
