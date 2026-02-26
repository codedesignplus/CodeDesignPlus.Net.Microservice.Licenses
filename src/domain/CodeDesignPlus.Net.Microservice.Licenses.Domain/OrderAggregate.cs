using System.Security.Cryptography.X509Certificates;
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
    /// Gets the unique identifier of the transaction provided by the external payment gateway (e.g., Stripe, PayPal).
    /// </summary>
    public Guid PaymentId { get; private set; }

    /// <summary>
    /// Gets the snapshot of the license details purchased in this order.
    /// This is an immutable copy and will not change even if the main License catalog is updated later.
    /// </summary>
    public License License { get; private set; } = null!;

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
    /// Gets the error message if the payment or order processing failed. Null if successful or pending.
    /// </summary>
    public string? Error { get; private set; }

    /// <summary>
    /// Indicates whether the entire order process (payment and internal validation) was successful.
    /// </summary>
    public bool IsSuccess { get; private set; }

    /// <summary>
    /// Gets the current state of the payment (e.g., Initiated, Pending, Succeeded, Failed).
    /// </summary>
    public PaymentStatus PaymentStatus { get; private set; }

    /// <summary>
    /// Creates a new Order instance with the initial status of 'Initiated'.
    /// Captures the immutable snapshots of the license, buyer, and tenant.
    /// </summary>
    /// <param name="id">The unique identifier to assign to the order.</param>
    /// <param name="paymentId">The external payment gateway transaction ID.</param>
    /// <param name="license">The snapshot of the purchased license.</param>
    /// <param name="paymentMethod">The payment method details.</param>
    /// <param name="buyer">The buyer's information.</param>
    /// <param name="tenantDetail">The tenant details to be provisioned.</param>
    /// <param name="createdBy">The identifier of the user or system creating the order.</param>
    /// <returns>A new <see cref="OrderAggregate"/> instance.</returns>
    /// <exception cref="DomainException">Thrown if any required parameter is missing or invalid.</exception>
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
    /// Updates the payment status of the order (usually called via a webhook from the payment gateway).
    /// If the status is 'Succeeded', it calculates the license expiration date and dispatches the 
    /// domain event to begin tenant provisioning.
    /// </summary>
    /// <param name="paymentStatus">The new status of the payment.</param>
    /// <param name="metadata">Additional data provided by the payment gateway (e.g., receipt URL, external references).</param>
    /// <param name="buyerId">The identifier of the buyer triggering this update.</param>
    public void SetPaymentStatus(PaymentStatus paymentStatus, Dictionary<string, string> metadata, Guid buyerId)
    {
        PaymentStatus = paymentStatus;

        if (paymentStatus == PaymentStatus.Succeeded)
        {
            var expirationDuration = License.BillingType == BillingTypeEnum.Monthly ? Duration.FromDays(30) : Duration.FromDays(365);
            var expirationDate = SystemClock.Instance.GetCurrentInstant().Plus(expirationDuration);

            var @event = OrderPaidAndReadyForProvisioningDomainEvent.Create(
                this.Id, 
                TenantDetail,
                LicenseTenant.Create(
                    License.Id,
                    License.Name,
                    expirationDate,
                    SystemClock.Instance.GetCurrentInstant(),
                    metadata
                ),
                buyerId
            );

            AddEvent(@event);
        }

        UpdatedAt = SystemClock.Instance.GetCurrentInstant();
    }
}