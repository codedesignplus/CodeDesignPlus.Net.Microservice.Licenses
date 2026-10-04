using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Emails.gRpc;
using CodeDesignPlus.Net.gRpc.Clients.Services.FileStorage;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.CompleteProvisioningStep;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.FailProvisioningStep;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;

/// <summary>
/// Once the payment is applied, ms-tenants and ms-users mark their provisioning steps on the order in parallel.
/// A full-document UpdateAsync written from a copy read earlier erased those steps, and purchases stayed "in
/// progress" until the reconciliation job (pendings/080). These tests pin that every order write after the
/// payment is a partial one, and that its side effects happen only when the write applied.
/// </summary>
public class OrderWritesTest
{
    private readonly Mock<IOrderRepository> repository = new();
    private readonly Mock<IPubSub> pubsub = new();
    private readonly Mock<ILiveChannelGrpc> liveChannel = new();
    private readonly Mock<IEmailGrpc> emailGrpc = new();
    private readonly Mock<IFileStorageGrpc> fileStorage = new();
    private readonly Mock<ICurrencyGrpc> currencyGrpc = new();

    private UpdateStateOrderCommandHandler PaymentHandler() => new(
        repository.Object, pubsub.Object, liveChannel.Object, emailGrpc.Object, fileStorage.Object, currencyGrpc.Object,
        new Mock<ILogger<UpdateStateOrderCommandHandler>>().Object);

    private void ReceiptCanBeGenerated()
    {
        currencyGrpc
            .Setup(x => x.GetCurrencyAsync(null, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CodeDesignPlus.Net.ValueObjects.Financial.Currency.Create(Guid.NewGuid(), "Peso Colombiano", "COP", "$", 2, 170));
        emailGrpc
            .Setup(x => x.GeneratePdfAsync(It.IsAny<GeneratePdfRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratePdfResponse { Success = true, PdfContent = Google.Protobuf.ByteString.CopyFrom([1, 2, 3]) });
        fileStorage
            .Setup(x => x.UploadAsync(It.IsAny<UploadFileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UploadFileRequest request, CancellationToken _) =>
                new UploadFileResponse { Id = request.Id, Target = request.Target, FileName = request.FileName });
    }

    /// <summary>
    /// The receipt is stored through ms-filestorage, which keeps its record, and the order points at that record
    /// (pendings/260).
    /// </summary>
    [Fact]
    public async Task Handle_PaymentApproved_StoresTheReceiptThroughFileStorage()
    {
        var order = TestOrders.Build();
        repository.Setup(x => x.FindAsync<OrderAggregate>(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        repository.Setup(x => x.ApplyPaymentStatusAsync(order, PaymentStatus.Initiated, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        ReceiptCanBeGenerated();
        UploadFileRequest? stored = null;
        fileStorage
            .Setup(x => x.UploadAsync(It.IsAny<UploadFileRequest>(), It.IsAny<CancellationToken>()))
            .Callback((UploadFileRequest request, CancellationToken _) => stored = request)
            .ReturnsAsync((UploadFileRequest request, CancellationToken _) =>
                new UploadFileResponse { Id = request.Id, Target = request.Target, FileName = request.FileName });

        await PaymentHandler().Handle(new UpdateStateOrderCommand(order.Id, PaymentStatus.Succeeded), CancellationToken.None);

        Assert.NotNull(stored);
        Assert.Equal("licenses-pdf", stored.Target);
        Assert.Equal(order.TenantDetail.Id.ToString(), stored.Tenant);
        Assert.Equal(TestOrders.BuyerId.ToString(), stored.UploadedBy);
        Assert.Equal([1, 2, 3], stored.Content.ToByteArray());
        repository.Verify(x => x.AttachReceiptAsync(order.Id,
            It.Is<FileAttachment>(r => r.Id == Guid.Parse(stored.Id) && r.Target == "licenses-pdf" && r.Name == "PurchaseReceipt.pdf"),
            TestOrders.BuyerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_PaymentApproved_WritesOnlyThePaymentAndTheReceipt()
    {
        var order = TestOrders.Build();
        repository.Setup(x => x.FindAsync<OrderAggregate>(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        repository.Setup(x => x.ApplyPaymentStatusAsync(order, PaymentStatus.Initiated, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        ReceiptCanBeGenerated();

        await PaymentHandler().Handle(new UpdateStateOrderCommand(order.Id, PaymentStatus.Succeeded), CancellationToken.None);

        repository.Verify(x => x.ApplyPaymentStatusAsync(order, PaymentStatus.Initiated, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.AttachReceiptAsync(order.Id, It.IsAny<FileAttachment>(), TestOrders.BuyerId, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.UpdateAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PaymentApproved_PersistsThePaymentBeforePublishingTheProvisioning()
    {
        // If the event went out first, ms-tenants and ms-users could answer before the pending steps exist.
        var order = TestOrders.Build();
        var calls = new List<string>();
        repository.Setup(x => x.FindAsync<OrderAggregate>(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        repository.Setup(x => x.ApplyPaymentStatusAsync(order, PaymentStatus.Initiated, It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("apply")).ReturnsAsync(true);
        pubsub.Setup(x => x.PublishAsync(It.IsAny<IReadOnlyList<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("publish")).Returns(Task.CompletedTask);

        await PaymentHandler().Handle(new UpdateStateOrderCommand(order.Id, PaymentStatus.Succeeded), CancellationToken.None);

        Assert.Equal(["apply", "publish"], calls);
    }

    [Fact]
    public async Task Handle_SameResultDeliveredAgain_DoesNothing()
    {
        // A redelivery must not regenerate the receipt nor send the confirmation email twice.
        var order = TestOrders.Paid();
        repository.Setup(x => x.FindAsync<OrderAggregate>(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        await PaymentHandler().Handle(new UpdateStateOrderCommand(order.Id, PaymentStatus.Succeeded), CancellationToken.None);

        repository.Verify(x => x.ApplyPaymentStatusAsync(It.IsAny<OrderAggregate>(), It.IsAny<PaymentStatus>(), It.IsAny<CancellationToken>()), Times.Never);
        pubsub.Verify(x => x.PublishAsync(It.IsAny<IReadOnlyList<IDomainEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
        emailGrpc.Verify(x => x.GeneratePdfAsync(It.IsAny<GeneratePdfRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_AnotherWriterAppliedThePaymentFirst_DoesNotPublishNorSendTheReceipt()
    {
        // The webhook and the reconciliation job can apply the same payment at once: only the winner publishes.
        var order = TestOrders.Build();
        repository.Setup(x => x.FindAsync<OrderAggregate>(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        repository.Setup(x => x.ApplyPaymentStatusAsync(order, PaymentStatus.Initiated, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        await PaymentHandler().Handle(new UpdateStateOrderCommand(order.Id, PaymentStatus.Succeeded), CancellationToken.None);

        pubsub.Verify(x => x.PublishAsync(It.IsAny<IReadOnlyList<IDomainEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
        emailGrpc.Verify(x => x.GeneratePdfAsync(It.IsAny<GeneratePdfRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_LastStepCompleted_OnlyChangesTheStatusAndPublishesOnce()
    {
        // The atomic push returns the document as it is now: both steps done, status still in progress.
        var order = TestOrders.Paid();
        order.ProvisioningHistory.Add(new ProvisioningStep("TenantProvisioning", ProvisioningStepStatus.Completed, SystemClock.Instance.GetCurrentInstant()));
        order.ProvisioningHistory.Add(new ProvisioningStep("UserProvisioning", ProvisioningStepStatus.Completed, SystemClock.Instance.GetCurrentInstant()));
        var inProgress = order;

        repository.Setup(x => x.CompleteProvisioningStepAtomicAsync(order.Id, "UserProvisioning", It.IsAny<CancellationToken>())).ReturnsAsync(inProgress);
        repository.Setup(x => x.MarkProvisioningCompletedAsync(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var handler = new CompleteProvisioningStepCommandHandler(repository.Object, pubsub.Object, liveChannel.Object);
        await handler.Handle(new CompleteProvisioningStepCommand(order.Id, "UserProvisioning"), CancellationToken.None);

        repository.Verify(x => x.MarkProvisioningCompletedAsync(order.Id, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.UpdateAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        pubsub.Verify(x => x.PublishAsync(It.IsAny<IReadOnlyList<IDomainEvent>>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_OrderAlreadyCompletedByTheOtherStep_DoesNotPublishAgain()
    {
        var inProgress = TestOrders.Paid();
        inProgress.ProvisioningHistory.Add(new ProvisioningStep("TenantProvisioning", ProvisioningStepStatus.Completed, SystemClock.Instance.GetCurrentInstant()));
        inProgress.ProvisioningHistory.Add(new ProvisioningStep("UserProvisioning", ProvisioningStepStatus.Completed, SystemClock.Instance.GetCurrentInstant()));

        repository.Setup(x => x.CompleteProvisioningStepAtomicAsync(inProgress.Id, "TenantProvisioning", It.IsAny<CancellationToken>())).ReturnsAsync(inProgress);
        repository.Setup(x => x.MarkProvisioningCompletedAsync(inProgress.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = new CompleteProvisioningStepCommandHandler(repository.Object, pubsub.Object, liveChannel.Object);
        await handler.Handle(new CompleteProvisioningStepCommand(inProgress.Id, "TenantProvisioning"), CancellationToken.None);

        pubsub.Verify(x => x.PublishAsync(It.IsAny<IReadOnlyList<IDomainEvent>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(true, 1)]
    [InlineData(false, 0)]
    public async Task Handle_StepFailed_WritesOnlyTheFailureAndPublishesWhenItApplied(bool applied, int publishes)
    {
        var order = TestOrders.Paid();
        repository.Setup(x => x.FindAsync<OrderAggregate>(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        repository.Setup(x => x.FailProvisioningAsync(order.Id, It.IsAny<ProvisioningStep>(), TestOrders.BuyerId, It.IsAny<CancellationToken>())).ReturnsAsync(applied);

        var handler = new FailProvisioningStepCommandHandler(repository.Object, pubsub.Object, liveChannel.Object);
        await handler.Handle(new FailProvisioningStepCommand(order.Id, "TenantProvisioning", "boom"), CancellationToken.None);

        repository.Verify(x => x.FailProvisioningAsync(order.Id,
            It.Is<ProvisioningStep>(s => s.StepName == "TenantProvisioning" && s.Status == ProvisioningStepStatus.Failed),
            TestOrders.BuyerId, It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.UpdateAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        pubsub.Verify(x => x.PublishAsync(It.IsAny<IReadOnlyList<IDomainEvent>>(), It.IsAny<CancellationToken>()), Times.Exactly(publishes));
    }
}
