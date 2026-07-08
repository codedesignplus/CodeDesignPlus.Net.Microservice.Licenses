using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Payment;
using CodeDesignPlus.Net.ValueObjects.User;
using ProvisioningStep = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.ProvisioningStep;

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
    /// Gets the complete immutable snapshot of the license (including modules, attributes, descriptions) purchased in this order.
    /// This snapshot reflects exactly what the customer bought, regardless of later catalog changes.
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
    /// Gets the overall provisioning status of the order.
    /// </summary>
    public ProvisioningStatus ProvisioningStatus { get; private set; }

    /// <summary>
    /// Gets the history of provisioning steps with their statuses.
    /// </summary>
    public List<ProvisioningStep> ProvisioningHistory { get; private set; } = [];

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
            License.Modules,
            License.Attributes
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
            ProvisioningStatus = ProvisioningStatus.PaymentPending,
            TenantDetail = tenantDetail,
            IsActive = true,
            CreatedAt = SystemClock.Instance.GetCurrentInstant(),
            CreatedBy = createdBy
        };

        return aggregate;
    }

    /// <summary>
    /// Updates the payment status. When succeeded, dispatches the provisioning domain event.
    /// The license snapshot (including modules and attributes) is already complete from order creation.
    /// </summary>
    /// <param name="paymentStatus">The new payment status.</param>
    /// <param name="buyerId">The buyer's identifier.</param>
    public void SetPaymentStatus(PaymentStatus paymentStatus, Guid buyerId)
    {
        // Idempotent: if already at this status, skip (message redelivery)
        if (PaymentStatus == paymentStatus)
            return;

        PaymentStatus = paymentStatus;

        if (paymentStatus == PaymentStatus.Succeeded)
        {
            IsSuccess = true;
            ProvisioningStatus = ProvisioningStatus.InProgress;

            var now = SystemClock.Instance.GetCurrentInstant();
            ProvisioningHistory.Add(new ProvisioningStep("Payment", ProvisioningStepStatus.Completed, now));
            ProvisioningHistory.Add(new ProvisioningStep("TenantProvisioning", ProvisioningStepStatus.Pending, now));
            ProvisioningHistory.Add(new ProvisioningStep("UserProvisioning", ProvisioningStepStatus.Pending, now));

            var @event = OrderPaidAndReadyForProvisioningDomainEvent.Create(
                this.Id,
                TenantDetail,
                LicenseTenant.Create(
                    License.Id,
                    License.Name,
                    SystemClock.Instance.GetCurrentInstant(),
                    SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(License.BillingType == BillingType.Monthly ? 30 : 365)),
                    License.Modules,
                    License.Attributes
                ),
                buyerId
            );

            AddEvent(@event);
        }
        else if (paymentStatus == PaymentStatus.Failed || paymentStatus == PaymentStatus.Expired)
        {
            var now = SystemClock.Instance.GetCurrentInstant();
            ProvisioningStatus = ProvisioningStatus.PaymentFailed;
            ProvisioningHistory.Add(new ProvisioningStep("Payment", ProvisioningStepStatus.Failed, now));
        }

        UpdatedAt = SystemClock.Instance.GetCurrentInstant();
    }

    public void CompleteProvisioningStep(string stepName, Guid updatedBy)
    {
        var existingCompleted = ProvisioningHistory.Any(s => s.StepName == stepName && s.Status == ProvisioningStepStatus.Completed);
        if (existingCompleted) return;

        ProvisioningHistory.Add(new ProvisioningStep(stepName, ProvisioningStepStatus.Completed, SystemClock.Instance.GetCurrentInstant()));
        UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        EvaluateCompletion();
    }

    public void FailProvisioningStep(string stepName, string error, Guid updatedBy)
    {
        ProvisioningHistory.Add(new ProvisioningStep(stepName, ProvisioningStepStatus.Failed, SystemClock.Instance.GetCurrentInstant(), error));
        ProvisioningStatus = ProvisioningStatus.PartiallyFailed;
        UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        AddEvent(OrderProvisioningFailedDomainEvent.Create(Id, stepName, error, updatedBy));
    }

    public void SetProvisioningCompleted()
    {
        if (ProvisioningStatus == ProvisioningStatus.Completed)
            return;

        ProvisioningStatus = ProvisioningStatus.Completed;
        UpdatedAt = SystemClock.Instance.GetCurrentInstant();
        AddEvent(OrderProvisioningCompletedDomainEvent.Create(Id, Buyer.BuyerId, TenantDetail.Id));
    }

    private void EvaluateCompletion()
    {
        var required = new[] { "TenantProvisioning", "UserProvisioning" };
        var completed = ProvisioningHistory
            .Where(s => s.Status == ProvisioningStepStatus.Completed)
            .Select(s => s.StepName)
            .ToHashSet();

        if (required.All(completed.Contains))
        {
            ProvisioningStatus = ProvisioningStatus.Completed;
            AddEvent(OrderProvisioningCompletedDomainEvent.Create(Id, Buyer.BuyerId, TenantDetail.Id));
        }
    }
}
