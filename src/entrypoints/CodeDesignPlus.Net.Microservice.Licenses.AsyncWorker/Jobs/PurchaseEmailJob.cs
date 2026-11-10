using CodeDesignPlus.Net.Hangfire.Abstractions;
using CodeDesignPlus.Net.Hangfire.Abstractions.Attributes;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;
using Hangfire;
using Microsoft.Extensions.Logging;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Jobs;

/// <summary>
/// Recurring job (every 5 minutes) that ends the wait of the email of a paid order for its electronic invoice (pendings/661,
/// step 4): when the invoice was not validated in time, the email goes with the receipt alone, and the invoice follows in its
/// own email when it is validated.
/// </summary>
/// <remarks>
/// The record of each order is on the order itself (<c>ReceiptEmailWaitsUntil</c>, <c>ReceiptEmailSentAt</c>,
/// <c>InvoiceEmailSentAt</c>): the job answers for the run and the order for the item (rule 23). The send is reserved on the
/// order with a conditional write, so the consumer of the invoice and this job never send it twice.
/// </remarks>
/// <param name="orders">The repository of orders.</param>
/// <param name="purchaseEmails">The email of the purchase.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
[RecurringJobOptions("*/5 * * * *", jobId: "purchase-email-job")]
public class PurchaseEmailJob(IOrderRepository orders, IPurchaseEmails purchaseEmails, IClock clock, ILogger<PurchaseEmailJob> logger) : IRecurrentJob
{
    /// <summary>
    /// How many orders a round sends at most; the rest wait for the next round.
    /// </summary>
    public const int BatchSize = 50;

    /// <inheritdoc/>
    [DisableConcurrentExecution(timeoutInSeconds: 5 * 60)]
    public async Task ExecuteAsync(IJobCancellationToken jobCancellationToken)
    {
        var cancellationToken = jobCancellationToken.ShutdownToken;
        var overdue = await orders.FindReceiptEmailsOverdueAsync(clock.GetCurrentInstant(), BatchSize, cancellationToken);
        var failed = 0;

        foreach (var order in overdue)
        {
            try
            {
                await purchaseEmails.SendDueAsync(order.Id, cancellationToken);
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                failed++;

                logger.LogError(exception, "The email of order {OrderId} could not be sent after waiting for its invoice; it is retried in the next round.", order.Id);
            }
        }

        // Without this the round shows in green in the dashboard although no email went out.
        if (failed > 0)
        {
            throw new InvalidOperationException($"{failed} of {overdue.Count} emails of paid orders could not be sent.");
        }
    }
}
