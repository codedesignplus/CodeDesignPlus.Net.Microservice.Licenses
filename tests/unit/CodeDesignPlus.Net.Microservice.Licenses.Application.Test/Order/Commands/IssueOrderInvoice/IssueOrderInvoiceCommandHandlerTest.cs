using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.ElectronicInvoicing;
using CodeDesignPlus.Net.gRpc.Clients.Services.Notification;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.IssueOrderInvoice;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order.Commands.IssueOrderInvoice;

/// <summary>
/// A paid order asks ms-electronicinvoicing for its documents (pendings/661).
/// </summary>
public class IssueOrderInvoiceCommandHandlerTest
{
    private static readonly Guid Administrators = Guid.Parse("20000000-0000-0000-0000-000000000010");

    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<IElectronicInvoicingGrpc> invoicing = new();
    private readonly Mock<IInboxGrpc> inbox = new();
    private readonly List<IssueDocumentRequest> issued = [];
    private readonly List<string> calls = [];
    private PlatformInvoicingOptions settings = PlatformInvoiceTest.Options();

    public IssueOrderInvoiceCommandHandlerTest()
    {
        invoicing
            .Setup(x => x.IssueDocumentAsync(It.IsAny<IssueDocumentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IssueDocumentRequest, CancellationToken>((request, _) => { issued.Add(request); calls.Add($"issue:{request.Type}"); })
            .ReturnsAsync((IssueDocumentRequest request, CancellationToken _) => new IssueDocumentResponse { Id = request.Id, Status = DocumentStatus.Draft });

        orders
            .Setup(x => x.RequestElectronicDocumentsAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("request"))
            .ReturnsAsync(true);
    }

    private IssueOrderInvoiceCommandHandler Handler() => new(
        orders.Object, invoicing.Object, inbox.Object, Microsoft.Extensions.Options.Options.Create(settings),
        Microsoft.Extensions.Options.Options.Create(new PlatformNoticeOptions { PlatformAdministratorRole = Administrators }),
        Mock.Of<ILogger<IssueOrderInvoiceCommandHandler>>());

    private void Stored(params OrderAggregate[] stored)
    {
        foreach (var order in stored)
        {
            orders.Setup(x => x.FindAsync<OrderAggregate>(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        }
    }

    private static void Requested(OrderAggregate order) => order.RequestElectronicDocuments(
        OrderElectronicDocument.Requested(PlatformInvoice.InvoiceIdOf(order.Id), PlatformInvoice.InvoiceReferenceOf(PlatformInvoiceTest.Options(), order.Id), null, SystemClock.Instance.GetCurrentInstant()), []);

    [Fact]
    public async Task Handle_PaidPurchase_WritesTheRequestBeforeAskingForTheInvoice()
    {
        // Arrange
        var order = PlatformInvoiceTest.PaidPurchase();
        Stored(order);

        // Act
        await Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None);

        // Assert
        Assert.Equal(["request", "issue:Invoice"], calls);
    }

    [Fact]
    public async Task Handle_PaidPurchase_AsksForTheInvoiceWithTheIdOfTheOrder()
    {
        // Arrange
        var order = PlatformInvoiceTest.PaidPurchase();
        Stored(order);

        // Act
        await Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None);

        // Assert
        Assert.Equal((PlatformInvoice.InvoiceIdOf(order.Id).ToString(), ElectronicDocumentState.Requested), (issued.Single().Id, order.Invoice!.State));
    }

    /// <summary>
    /// A redelivery after the first call failed asks again for the same document, and does not write the order again.
    /// </summary>
    [Fact]
    public async Task Handle_InvoiceAlreadyRequested_AsksAgainForTheSameDocument()
    {
        // Arrange
        var order = PlatformInvoiceTest.PaidPurchase();
        Requested(order);
        Stored(order);

        // Act
        await Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None);

        // Assert
        Assert.Equal(["issue:Invoice"], calls);
    }

    [Fact]
    public async Task Handle_InvoiceAlreadyValidated_AsksForNothing()
    {
        // Arrange
        var order = PlatformInvoiceTest.PaidPurchase();
        order.RequestElectronicDocuments(PlatformInvoiceTest.ValidatedInvoiceOf(order), []);
        Stored(order);

        // Act
        await Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None);

        // Assert
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Handle_InvoicingOff_AsksForNothing()
    {
        // Arrange
        settings = new PlatformInvoicingOptions { Enable = false };

        // Act
        await Handler().Handle(new IssueOrderInvoiceCommand(Guid.NewGuid()), CancellationToken.None);

        // Assert
        Assert.Empty(calls);
    }

    [Fact]
    public async Task Handle_OrderNotPaid_AsksForNothing()
    {
        // Arrange
        var order = TestOrders.Build();
        Stored(order);

        // Act
        await Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None);

        // Assert
        Assert.Empty(calls);
    }

    /// <summary>
    /// A change whose previous invoice is validated: first the credit note on it, then the invoice, which waits for the
    /// note.
    /// </summary>
    [Fact]
    public async Task Handle_PaidChange_AsksForTheCreditNoteFirstAndTheInvoiceAfterIt()
    {
        // Arrange
        var previous = PlatformInvoiceTest.PaidPurchase();
        previous.RequestElectronicDocuments(PlatformInvoiceTest.ValidatedInvoiceOf(previous), []);
        var order = PlatformInvoiceTest.PaidChange(previous);
        Stored(previous, order);

        // Act
        await Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None);

        // Assert
        Assert.Equal(
            ("request,issue:CreditNote,issue:Invoice", issued[0].Id),
            (string.Join(',', calls), issued[1].PrecededBy));
    }

    /// <summary>
    /// The previous order has no invoice in the system (invoiced by hand or before pendings/661): the note is not sent,
    /// it stays for review and the platform administrator is told; the invoice of the sale goes anyway.
    /// </summary>
    [Fact]
    public async Task Handle_ChangeOfAnOrderWithoutInvoice_LeavesTheNoteForReviewAndInvoicesTheSale()
    {
        // Arrange
        var previous = PlatformInvoiceTest.PaidPurchase();
        var order = PlatformInvoiceTest.PaidChange(previous);
        Stored(previous, order);

        // Act
        await Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None);

        // Assert
        Assert.Equal(
            (ElectronicDocumentState.NeedsReview, "request,issue:Invoice", false),
            (order.CreditNotes!.Single().State, string.Join(',', calls), issued.Single().HasPrecededBy));
    }

    [Fact]
    public async Task Handle_ChangeOfAnOrderWithoutInvoice_TellsThePlatformAdministrator()
    {
        // Arrange
        var previous = PlatformInvoiceTest.PaidPurchase();
        var order = PlatformInvoiceTest.PaidChange(previous);
        Stored(previous, order);

        // Act
        await Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None);

        // Assert
        inbox.Verify(x => x.NotifyAsync(It.Is<NotificationRequest>(r => r.Kind == PlatformNotificationKinds.LicenseInvoiceRejected && r.Audience.Values.Contains(Administrators.ToString())), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The previous invoice is still waiting for the tax authority: nothing is written nor sent, and the message fails to be
    /// retried later.
    /// </summary>
    [Fact]
    public async Task Handle_ChangeOfAnOrderWhoseInvoiceIsWaiting_FailsWithoutWritingAnything()
    {
        // Arrange
        var previous = PlatformInvoiceTest.PaidPurchase();
        Requested(previous);
        var order = PlatformInvoiceTest.PaidChange(previous);
        Stored(previous, order);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None));

        // Assert
        Assert.Empty(calls);
    }

    /// <summary>
    /// The invoice of a sale goes even if ms-electronicinvoicing fails afterwards: the failure retries the message, the
    /// purchase is not touched.
    /// </summary>
    [Fact]
    public async Task Handle_ElectronicInvoicingFails_TheErrorRetriesTheMessage()
    {
        // Arrange
        var order = PlatformInvoiceTest.PaidPurchase();
        Stored(order);
        invoicing.Setup(x => x.IssueDocumentAsync(It.IsAny<IssueDocumentRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new Grpc.Core.RpcException(new Grpc.Core.Status(Grpc.Core.StatusCode.Unavailable, "down")));

        // Act & Assert
        await Assert.ThrowsAsync<Grpc.Core.RpcException>(() => Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None));
    }
}
