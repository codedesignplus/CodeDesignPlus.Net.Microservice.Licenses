using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Payment;
using CodeDesignPlus.Net.ValueObjects.User;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

/// <summary>
/// Represents the Aggregate Root for an Order.
/// Manages the lifecycle of a license purchase, payment tracking, and triggers the tenant provisioning process upon success.
/// This aggregate acts as an immutable snapshot of the license, buyer, and tenant details at the exact moment of purchase.
/// </summary>
/// <param name="id">The unique identifier of the order.</param>
public class OrderAggregate(Guid id) : AggregateRootBase(id)
{
    /// <summary>
    /// Gets the unique identifier of the transaction provided by the external payment gateway.
    /// </summary>
    public Guid PaymentId { get; private set; }

    /// <summary>
    /// Gets the snapshot of the license details (price, billing type) purchased in this order.
    /// </summary>
    public License License { get; private set; } = null!;

    /// <summary>
    /// Gets the snapshot of the modules included in the license at the time of purchase.
    /// Immutable — reflects what the customer actually bought, regardless of later catalog changes.
    /// </summary>
    public List<LicenseModule> LicenseModules { get; private set; } = [];

    /// <summary>
    /// Gets the snapshot of the payment method used by the buyer for this order.
    /// </summary>
    public PaymentMethod PaymentMethod { get; private set; } = null!;

    /// <summary>
    /// Gets the snapshot of the buyer's information at the time of purchase.
    /// </summary>
    public Buyer Buyer { get; private set; } = null!;

    /// <summary>
    /// Gets the details of the tenant workspace that will be provisioned once the order is successful.
    /// </summary>
    public Tenant TenantDetail { get; private set; } = null!;

    /// <summary>
    /// Gets the error message if the payment or order processing failed.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Indicates whether the order process was successful.
    /// </summary>
    public bool IsSuccess { get; private set; }

    /// <summary>
    /// Gets the current state of the payment.
    /// </summary>
    public PaymentStatus PaymentStatus { get; private set; }

    /// <summary>
    /// Returns the immutable LicenseTenant snapshot from this order.
    /// Used by the gRPC service to return license info to the Security middleware.
    /// </summary>
    public LicenseTenant GetLicenseTenant()
    {
        DomainGuard.IsFalse(IsSuccess, Errors.OrderIsNotSucceeded);

        return LicenseTenant.Create(
            License.Id,
            License.Name,
            CreatedAt,
            CreatedAt.Plus(Duration.FromDays(License.BillingType == BillingType.Monthly ? 30 : 365)),
            LicenseModules,
            new Dictionary<string, string>()
        );
    }

    /// <summary>
    /// Creates a new Order instance with the initial status of 'Initiated'.
    /// </summary>
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

    /// <summary>
    /// Updates the payment status. When succeeded, captures the license modules snapshot
    /// and dispatches the provisioning domain event.
    /// </summary>
    /// <param name="paymentStatus">The new payment status.</param>
    /// <param name="metadata">Additional gateway metadata.</param>
    /// <param name="buyerId">The buyer's identifier.</param>
    /// <param name="licenseModules">Modules of the purchased license (snapshot at purchase time).</param>
    public void SetPaymentStatus(PaymentStatus paymentStatus, Dictionary<string, string> metadata, Guid buyerId, List<ModuleEntity> licenseModules)
    {
        PaymentStatus = paymentStatus;

        if (paymentStatus == PaymentStatus.Succeeded)
        {
            IsSuccess = true;

            LicenseModules = (licenseModules ?? [])
                .Select(m => new LicenseModule(m.Id, m.Name, m.Description))
                .ToList();

            var @event = OrderPaidAndReadyForProvisioningDomainEvent.Create(
                this.Id,
                TenantDetail,
                LicenseTenant.Create(
                    License.Id,
                    License.Name,
                    SystemClock.Instance.GetCurrentInstant(),
                    SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(License.BillingType == BillingType.Monthly ? 30 : 365)),
                    LicenseModules,
                    metadata
                ),
                buyerId
            );

            AddEvent(@event);
        }

        UpdatedAt = SystemClock.Instance.GetCurrentInstant();
    }
}
