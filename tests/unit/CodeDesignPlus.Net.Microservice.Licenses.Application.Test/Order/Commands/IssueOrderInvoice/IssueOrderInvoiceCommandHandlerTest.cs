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
using Microsoft.Extensions.Configuration;
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

    /// <summary>
    /// The deployed rule: the tax id carries a check digit, computed with the weights of the tax authority.
    /// </summary>
    private readonly TenantDocumentOptions tenantDocument = new() { CheckDigitDocumentTypes = ["NIT"], CheckDigitWeights = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71] };

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
        Microsoft.Extensions.Options.Options.Create(tenantDocument),
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

    /// <summary>
    /// The order carries a tax id without its check digit (Kappali pendings/744): the invoice is not asked of
    /// ms-electronicinvoicing, which would issue it with the digit empty. It stays for review with the reason.
    /// </summary>
    [Theory]
    [InlineData("901777333")]
    [InlineData("901777333-5")]
    [InlineData("9017773330")]
    [InlineData("901.777.333-0")]
    public async Task Handle_BuyerTaxIdNotWrittenWithItsCheckDigit_LeavesTheInvoiceForReviewAndAsksForNothing(string numberDocument)
    {
        // Arrange
        var order = PlatformInvoiceTest.PaidPurchase(tenant: PlatformInvoiceTest.TenantWithNit(numberDocument));
        Stored(order);

        // Act
        await Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None);

        // Assert
        Assert.Equal(
            (ElectronicDocumentState.NeedsReview, PlatformInvoice.InvoiceIdOf(order.Id), "request"),
            (order.Invoice!.State, order.Invoice.DocumentId, string.Join(',', calls)));
    }

    /// <summary>
    /// The reason kept on the order says which document it is and what is wrong with it, in the language the platform
    /// administrator reads.
    /// </summary>
    [Fact]
    public async Task Handle_BuyerTaxIdWithoutCheckDigit_KeepsTheReasonOnTheOrder()
    {
        // Arrange
        var order = PlatformInvoiceTest.PaidPurchase(tenant: PlatformInvoiceTest.TenantWithNit("901777333"));
        Stored(order);

        // Act
        await Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None);

        // Assert
        Assert.Equal(
            "El documento de la copropiedad, «901777333», no sirve para facturar. Este tipo de documento se escribe con guion y dígito de verificación. Si 901777333 es el número sin el dígito, escríbelo así: 901777333-0.",
            Assert.Single(order.Invoice!.Reasons));
    }

    [Fact]
    public async Task Handle_BuyerTaxIdWithoutCheckDigit_TellsThePlatformAdministratorWhy()
    {
        // Arrange
        var order = PlatformInvoiceTest.PaidPurchase(tenant: PlatformInvoiceTest.TenantWithNit("901777333-5"));
        Stored(order);
        NotificationRequest? notice = null;
        inbox.Setup(x => x.NotifyAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>())).Callback<NotificationRequest, CancellationToken>((request, _) => notice = request);

        // Act
        await Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None);

        // Assert
        Assert.Equal(
            (PlatformNotificationKinds.LicenseInvoiceRejected, true, "Factura electrónica pendiente de revisión", "La orden de Conjunto Las Acacias se pagó, pero su factura electrónica no se expidió. El documento de la copropiedad, «901777333-5», no sirve para facturar. El dígito de verificación no corresponde: el de 901777333 es 0, no 5. Escríbelo así: 901777333-0. Pide a la copropiedad que corrija su documento en Organización y expide la factura a mano."),
            (notice!.Kind, notice.Audience.Values.Contains(Administrators.ToString()), notice.Title, notice.Body));
    }

    /// <summary>
    /// The credit notes carry the same buyer: none is sent either, and the previous invoice still waiting for the tax
    /// authority does not make the message fail, because nothing is going to be asked.
    /// </summary>
    [Fact]
    public async Task Handle_ChangeWithBuyerTaxIdWithoutCheckDigit_LeavesItsCreditNotesForReviewToo()
    {
        // Arrange
        var previous = PlatformInvoiceTest.PaidPurchase();
        Requested(previous);
        var order = PlatformInvoiceTest.PaidChange(previous, tenant: PlatformInvoiceTest.TenantWithNit("901777333"));
        Stored(previous, order);

        // Act
        await Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None);

        // Assert
        Assert.Equal(
            (ElectronicDocumentState.NeedsReview, ElectronicDocumentState.NeedsReview, previous.Id, "request"),
            (order.Invoice!.State, order.CreditNotes!.Single().State, order.CreditNotes!.Single().CorrectedOrderId, string.Join(',', calls)));
    }

    /// <summary>
    /// A redelivery finds the invoice already left for review: it asks for nothing and does not tell again.
    /// </summary>
    [Fact]
    public async Task Handle_InvoiceAlreadyLeftForReview_AsksForNothingAndDoesNotTellAgain()
    {
        // Arrange
        var order = PlatformInvoiceTest.PaidPurchase(tenant: PlatformInvoiceTest.TenantWithNit("901777333"));
        Stored(order);
        await Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None);
        calls.Clear();
        inbox.Invocations.Clear();

        // Act
        await Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None);

        // Assert
        Assert.Equal((0, 0), (calls.Count, inbox.Invocations.Count));
    }

    /// <summary>
    /// The tax id as ms-tenants has it, with its dash and its digit: the invoice goes with the number and the check digit
    /// apart, which is how the tax authority wants the buyer.
    /// </summary>
    [Fact]
    public async Task Handle_BuyerTaxIdWithItsCheckDigit_AsksForTheInvoiceWithTheDigitApart()
    {
        // Arrange
        var order = PlatformInvoiceTest.PaidPurchase(tenant: PlatformInvoiceTest.TenantWithNit("901777333-0"));
        Stored(order);

        // Act
        await Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None);

        // Assert
        var buyer = issued.Single().Content.Buyer;

        Assert.Equal(("901777333", "0", ElectronicDocumentState.Requested), (buyer.IdentificationNumber, buyer.CheckDigit, order.Invoice!.State));
    }

    /// <summary>
    /// A document type without check digit is invoiced as it comes: the rule is only for the types the configuration names.
    /// </summary>
    [Fact]
    public async Task Handle_BuyerDocumentOfATypeWithoutCheckDigit_AsksForTheInvoice()
    {
        // Arrange
        var order = PlatformInvoiceTest.PaidPurchase(tenant: PlatformInvoiceTest.TenantWithNit("1023456781", "CC"));
        Stored(order);

        // Act
        await Handler().Handle(new IssueOrderInvoiceCommand(order.Id), CancellationToken.None);

        // Assert
        Assert.Equal(("1023456781", false), (issued.Single().Content.Buyer.IdentificationNumber, issued.Single().Content.Buyer.HasCheckDigit));
    }

    /// <summary>
    /// The invoice is issued by the worker, so the worker has to know which types carry a check digit: without the section
    /// the rule checks nothing and a tax id without its digit would go out. The same deployed value as the API and
    /// ms-tenants.
    /// </summary>
    [Fact]
    public void Appsettings_WorkerTenantDocument_IsTheSharedDeployedValue()
    {
        // Arrange
        var directory = new System.IO.DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !System.IO.File.Exists(System.IO.Path.Combine(directory.FullName, "CodeDesignPlus.Net.Microservice.Licenses.sln")))
        {
            directory = directory.Parent;
        }

        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder().AddJsonFile(System.IO.Path.Combine(directory!.FullName, "src", "entrypoints", "CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker", "appsettings.json")).Build();

        // Act
        var options = Microsoft.Extensions.Configuration.ConfigurationBinder.Get<TenantDocumentOptions>(configuration.GetSection(TenantDocumentOptions.Section));

        // Assert
        Assert.Equal(
            ("31", "3,7,13,17,19,23,29,37,41,43,47,53,59,67,71"),
            (string.Join(',', options!.CheckDigitDocumentTypes), string.Join(',', options.CheckDigitWeights)));
    }
}
