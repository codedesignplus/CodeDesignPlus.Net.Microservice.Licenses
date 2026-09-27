using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.Payment;
using CodeDesignPlus.Net.Hangfire.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Jobs;
using CodeDesignPlus.Net.Microservice.Licenses.Domain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;
using CodeDesignPlus.Net.Core.Abstractions;
using CodeDesignPlus.Net.PubSub.Abstractions;
using Hangfire;
using MediatR;
using Xunit;
using DomainPaymentStatus = CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums.PaymentStatus;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Test.Jobs;

/// <summary>
/// The reconciliation job republished the provisioning event and THEN saved the whole order to count the attempt.
/// ms-tenants and ms-users answer in seconds and mark their steps on the order, so that late save could erase
/// them (pendings/080). The attempt is now counted first, with a partial write, and nothing saves the whole order.
/// </summary>
public class OrderReconciliationJobTest
{
    private readonly Mock<IOrderRepository> repository = new();
    private readonly Mock<IPubSub> pubsub = new();
    private readonly Mock<IPaymentGrpc> paymentGrpc = new();
    private readonly Mock<IMediator> mediator = new();

    private OrderReconciliationJob Job() => new(
        repository.Object, pubsub.Object, paymentGrpc.Object, mediator.Object, new Mock<ILogger<OrderReconciliationJob>>().Object);

    private static IJobCancellationToken Token()
    {
        var token = new Mock<IJobCancellationToken>();
        token.SetupGet(x => x.ShutdownToken).Returns(CancellationToken.None);
        return token.Object;
    }

    private void Stuck(ProvisioningStatus status, params OrderAggregate[] orders)
    {
        repository.Setup(x => x.FindStuckOrdersAsync(It.IsAny<ProvisioningStatus>(), It.IsAny<Duration>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        repository.Setup(x => x.FindStuckOrdersAsync(status, It.IsAny<Duration>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. orders]);
    }

    [Fact]
    public async Task ExecuteAsync_StuckInProgress_CountsTheAttemptBeforeRepublishingAndNeverSavesTheWholeOrder()
    {
        var order = TestOrders.Paid();
        Stuck(ProvisioningStatus.InProgress, order);
        var calls = new List<string>();
        repository.Setup(x => x.RegisterProvisioningAttemptAsync(order.Id, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("attempt")).ReturnsAsync(1);
        pubsub.Setup(x => x.PublishAsync(It.IsAny<OrderPaidAndReadyForProvisioningDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("publish")).Returns(Task.CompletedTask);

        await Job().ExecuteAsync(Token());

        Assert.Equal(["attempt", "publish"], calls);
        repository.Verify(x => x.UpdateAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_OrderNoLongerInProgress_DoesNotRepublish()
    {
        // It completed between the query and the write: nothing to retry.
        var order = TestOrders.Paid();
        Stuck(ProvisioningStatus.InProgress, order);
        repository.Setup(x => x.RegisterProvisioningAttemptAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync((int?)null);

        await Job().ExecuteAsync(Token());

        pubsub.Verify(x => x.PublishAsync(It.IsAny<OrderPaidAndReadyForProvisioningDomainEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_StuckPaymentApproved_GoesThroughTheSameCommandAsTheWebhook()
    {
        // Applying the payment by itself skipped the receipt and the confirmation email.
        var order = TestOrders.Build();
        Stuck(ProvisioningStatus.PaymentPending, order);
        var response = new GetPaymentStatusResponse();
        var status = typeof(GetPaymentStatusResponse).GetProperty(nameof(GetPaymentStatusResponse.Status))!;
        status.SetValue(response, Enum.ToObject(status.PropertyType, (int)DomainPaymentStatus.Succeeded));
        paymentGrpc.Setup(x => x.GetPaymentStatusAsync(order.PaymentId, It.IsAny<CancellationToken>())).ReturnsAsync(response);

        await Job().ExecuteAsync(Token());

        mediator.Verify(x => x.Send(
            It.Is<UpdateStateOrderCommand>(c => c.ReferenceId == order.Id && c.PaymentStatus == DomainPaymentStatus.Succeeded),
            It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.UpdateAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
