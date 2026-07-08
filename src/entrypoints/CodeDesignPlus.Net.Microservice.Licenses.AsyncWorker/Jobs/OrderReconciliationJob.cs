using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Hangfire.Abstractions;
using CodeDesignPlus.Net.Hangfire.Abstractions.Attributes;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Jobs;

/// <summary>
/// Recurring job (every 3 minutes) that detects and recovers stuck license orders.
/// - PaymentPending orders stuck > 10 min: queries ms-payments gRPC for actual payment status.
/// - InProgress orders stuck > 5 min: re-publishes provisioning event for downstream consumers.
/// </summary>
[RecurringJobOptions("*/3 * * * *", jobId: "order-reconciliation-job")]
public class OrderReconciliationJob(
    IOrderRepository orderRepository,
    IPubSub pubsub,
    IPaymentGrpc paymentGrpc,
    ILogger<OrderReconciliationJob> logger
) : IRecurrentJob
{
    private static readonly Duration PaymentPendingThreshold = Duration.FromMinutes(10);
    private static readonly Duration InProgressThreshold = Duration.FromMinutes(5);
    private const int BatchSize = 20;

    /// <inheritdoc/>
    [DisableConcurrentExecution(timeoutInSeconds: 5 * 60)]
    public async Task ExecuteAsync(IJobCancellationToken jobCancellationToken)
    {
        var ct = jobCancellationToken.ShutdownToken;

        await ReconcilePaymentPendingOrdersAsync(ct);
        await ReconcileInProgressOrdersAsync(ct);
    }

    private async Task ReconcilePaymentPendingOrdersAsync(CancellationToken cancellationToken)
    {
        var stuckOrders = await orderRepository.FindStuckOrdersAsync(
            ProvisioningStatus.PaymentPending, PaymentPendingThreshold, BatchSize, cancellationToken);

        foreach (var order in stuckOrders)
        {
            try
            {
                logger.LogWarning("Reconciling stuck PaymentPending order {OrderId} (PaymentId: {PaymentId})", order.Id, order.PaymentId);

                var response = await paymentGrpc.GetPaymentStatusAsync(order.PaymentId, cancellationToken);
                var paymentStatus = (PaymentStatus)(int)response.Status;

                if (paymentStatus == PaymentStatus.Succeeded)
                {
                    order.SetPaymentStatus(PaymentStatus.Succeeded, order.Buyer.BuyerId);
                    await orderRepository.UpdateAsync(order, cancellationToken);
                    await pubsub.PublishAsync(order.GetAndClearEvents(), cancellationToken);

                    logger.LogInformation("Reconciled order {OrderId}: payment was Succeeded, triggered provisioning.", order.Id);
                }
                else if (paymentStatus == PaymentStatus.Failed || paymentStatus == PaymentStatus.Expired)
                {
                    order.SetPaymentStatus(paymentStatus, order.Buyer.BuyerId);
                    await orderRepository.UpdateAsync(order, cancellationToken);
                    await pubsub.PublishAsync(order.GetAndClearEvents(), cancellationToken);

                    logger.LogInformation("Reconciled order {OrderId}: payment {Status}.", order.Id, paymentStatus);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to reconcile PaymentPending order {OrderId}. Will retry next cycle.", order.Id);
            }
        }
    }

    private async Task ReconcileInProgressOrdersAsync(CancellationToken cancellationToken)
    {
        var stuckOrders = await orderRepository.FindStuckOrdersAsync(
            ProvisioningStatus.InProgress, InProgressThreshold, BatchSize, cancellationToken);

        foreach (var order in stuckOrders)
        {
            try
            {
                var completedSteps = order.ProvisioningHistory
                    .Where(s => s.Status == ProvisioningStepStatus.Completed)
                    .Select(s => s.StepName)
                    .ToHashSet();

                var required = new[] { "TenantProvisioning", "UserProvisioning" };

                if (required.All(completedSteps.Contains))
                {
                    order.SetProvisioningCompleted();
                    await orderRepository.UpdateAsync(order, cancellationToken);
                    await pubsub.PublishAsync(order.GetAndClearEvents(), cancellationToken);

                    logger.LogInformation("Reconciled order {OrderId}: all steps completed, marked as Completed.", order.Id);
                    continue;
                }

                logger.LogWarning("Reconciling stuck InProgress order {OrderId}. Missing steps: {Missing}",
                    order.Id, string.Join(", ", required.Except(completedSteps)));

                var @event = OrderPaidAndReadyForProvisioningDomainEvent.Create(
                    order.Id,
                    order.TenantDetail,
                    LicenseTenant.Create(
                        order.License.Id,
                        order.License.Name,
                        order.CreatedAt,
                        order.CreatedAt.Plus(Duration.FromDays(order.License.BillingType == BillingType.Monthly ? 30 : 365)),
                        order.License.Modules,
                        order.License.Attributes
                    ),
                    order.Buyer.BuyerId
                );

                await pubsub.PublishAsync(@event, cancellationToken);

                // Touch UpdatedAt so we don't re-process on next cycle if still stuck
                order.UpdatedAt = SystemClock.Instance.GetCurrentInstant();
                await orderRepository.UpdateAsync(order, cancellationToken);

                logger.LogInformation("Re-published provisioning event for stuck order {OrderId}.", order.Id);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to reconcile InProgress order {OrderId}. Will retry next cycle.", order.Id);
            }
        }
    }
}
