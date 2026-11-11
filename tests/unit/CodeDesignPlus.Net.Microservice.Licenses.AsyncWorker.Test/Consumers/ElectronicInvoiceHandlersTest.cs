using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.IssueOrderInvoice;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.RecordElectronicDocument;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;
using CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Consumers;
using CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;
using MediatR;
using Microsoft.Extensions.Configuration;
using Xunit;
using GrpcPaymentStatus = CodeDesignPlus.Net.gRpc.Clients.Services.Payment.PaymentStatus;
using StoredFile = CodeDesignPlus.Net.ValueObjects.FileServer.File;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Test.Consumers;

/// <summary>
/// The worker invoices the paid orders and keeps what ms-electronicinvoicing answers (pendings/661).
/// </summary>
public class ElectronicInvoiceHandlersTest
{
    private readonly Mock<IMediator> mediator = new();
    private readonly List<object> sent = [];

    public ElectronicInvoiceHandlersTest()
    {
        mediator.Setup(x => x.Send(It.IsAny<IRequest>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest, CancellationToken>((command, _) => sent.Add(command))
            .Returns(Task.CompletedTask);
    }

    /// <summary>
    /// The approval brings with what the payer paid to the order, which the invoice needs.
    /// </summary>
    [Fact]
    public async Task UpdateOrderHandler_ApprovedCard_PassesTheInstrumentAndTheCardTypeToTheOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var orders = new Mock<IOrderRepository>();
        orders.Setup(x => x.ExistsAsync<OrderAggregate>(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var @event = new PaymentResponseAssociatedDomainEvent(Guid.NewGuid(), PayOrderCommandHandler.MODULE, orderId, GrpcPaymentStatus.Succeeded, new Dictionary<string, string?>(), null, paymentMethodType: "CARD", cardType: "CREDIT");

        // Act
        await new UpdateOrderHandler(Mock.Of<ILogger<UpdateOrderHandler>>(), mediator.Object, orders.Object).HandleAsync(@event, CancellationToken.None);

        // Assert
        var command = Assert.IsType<UpdateStateOrderCommand>(Assert.Single(sent));

        Assert.Equal(("CARD", "CREDIT"), (command.PaymentMethodType, command.CardType));
    }

    /// <summary>
    /// The twin of the payment event reads the instrument with the names ms-payments writes them.
    /// </summary>
    [Fact]
    public void PaymentResponseAssociated_JsonOfMsPayments_ReadsTheInstrument()
    {
        // Arrange
        var json = JsonSerializer.Serialize(new { AggregateId = Guid.NewGuid(), Module = "Licenses", ReferenceId = Guid.NewGuid(), Status = 2, Response = new Dictionary<string, string?>(), PaymentMethodType = "PSE", CardType = (string?)null });

        // Act
        var @event = JsonSerializer.Deserialize<PaymentResponseAssociatedDomainEvent>(json);

        // Assert
        Assert.Equal("PSE", @event.PaymentMethodType);
    }

    [Fact]
    public async Task IssuePurchaseInvoiceHandler_PaidPurchase_AsksForTheInvoiceOfTheOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var @event = new OrderPaidAndReadyForProvisioningDomainEvent(orderId, null!, null!, Guid.NewGuid());

        // Act
        await new IssuePurchaseInvoiceHandler(mediator.Object).HandleAsync(@event, CancellationToken.None);

        // Assert
        Assert.Equal(orderId, Assert.IsType<IssueOrderInvoiceCommand>(Assert.Single(sent)).OrderId);
    }

    /// <summary>
    /// The twin of the validation event of ms-electronicinvoicing reads what the order keeps.
    /// </summary>
    [Fact]
    public async Task ElectronicDocumentValidatedHandler_JsonOfElectronicInvoicing_KeepsTheNumberCufeAndFiles()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new
        {
            AggregateId = documentId,
            Tenant = Guid.Parse("00000000-e1ec-0000-0000-000000000001"),
            Reference = "license-order:x",
            Prefix = "SETP",
            Consecutive = 990000001L,
            Cufe = "cufe-1",
            IssueDate = new LocalDate(2026, 11, 10),
            ValidatedAt = Instant.FromUtc(2026, 11, 10, 15, 0),
            Pdf = StoredFile.Create(pdfId, "electronic-invoicing", "SETP990000001", ".pdf", "application/pdf", 1024)
        });

        var @event = JsonSerializer.Deserialize<ElectronicDocumentValidatedDomainEvent>(json);

        // Act
        await new ElectronicDocumentValidatedHandler(mediator.Object).HandleAsync(@event, CancellationToken.None);

        // Assert
        var command = Assert.IsType<RecordElectronicDocumentValidatedCommand>(Assert.Single(sent));

        Assert.Equal((documentId, "SETP", 990000001L, "cufe-1", (LocalDate?)new LocalDate(2026, 11, 10), pdfId, false), (command.DocumentId, command.Prefix, command.Consecutive, command.Cufe, command.IssueDate, command.Pdf!.Id, command.Xml is not null));
    }

    /// <summary>
    /// The origin 2 of ms-electronicinvoicing is the tax authority: the notice says the DIAN rejected it.
    /// </summary>
    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    public async Task ElectronicDocumentRejectedHandler_Origin_SaysWhetherTheDianRejectedIt(int origin, bool byTaxAuthority)
    {
        // Arrange
        var json = JsonSerializer.Serialize(new
        {
            AggregateId = Guid.NewGuid(),
            Reference = "license-order:x",
            Rejection = new { Origin = origin, Reasons = new[] { "FAK24" }, At = Instant.FromUtc(2026, 11, 10, 15, 0) }
        });

        var @event = JsonSerializer.Deserialize<ElectronicDocumentRejectedDomainEvent>(json);

        // Act
        await new ElectronicDocumentRejectedHandler(mediator.Object).HandleAsync(@event, CancellationToken.None);

        // Assert
        var command = Assert.IsType<RecordElectronicDocumentRejectedCommand>(Assert.Single(sent));

        Assert.Equal((byTaxAuthority, "FAK24"), (command.ByTaxAuthority, Assert.Single(command.Reasons)));
    }

    /// <summary>
    /// The twins of the events of ms-electronicinvoicing go through the bus and read back whole, with the serializer the bus
    /// uses (Newtonsoft through <c>CodeDesignPlus.Net.Serializers</c>).
    /// </summary>
    [Fact]
    public void ElectronicDocumentValidated_ThroughTheBus_ReadsBackWhole()
    {
        // Arrange
        var pdf = StoredFile.Create(Guid.NewGuid(), "electronic-invoicing", "SETP990000001", ".pdf", "application/pdf", 1024);
        var @event = new ElectronicDocumentValidatedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), "license-order:x", "SETP", 990000001L, "cufe-1", new LocalDate(2026, 11, 10), Instant.FromUtc(2026, 11, 10, 15, 0), null, pdf);
        var written = JsonSerializer.Serialize(@event);

        // Act
        var rewritten = JsonSerializer.Serialize(JsonSerializer.Deserialize<ElectronicDocumentValidatedDomainEvent>(written));

        // Assert
        Assert.Equal(written, rewritten);
    }

    [Fact]
    public void ElectronicDocumentRejected_ThroughTheBus_ReadsBackWhole()
    {
        // Arrange
        var @event = new ElectronicDocumentRejectedDomainEvent(Guid.NewGuid(), Guid.NewGuid(), "license-order:x", "SETP", 990000002L, new ElectronicDocumentRejection(ElectronicDocumentRejection.TaxAuthority, ["FAK24"], Instant.FromUtc(2026, 11, 10, 15, 0)));
        var written = JsonSerializer.Serialize(@event);

        // Act
        var rewritten = JsonSerializer.Serialize(JsonSerializer.Deserialize<ElectronicDocumentRejectedDomainEvent>(written));

        // Assert
        Assert.Equal(written, rewritten);
    }

    /// <summary>
    /// The appsettings of the worker carry the codes the accountant decided, and they validate.
    /// </summary>
    [Fact]
    public void Appsettings_PlatformInvoicing_CarriesTheCodesOfTheAccountant()
    {
        // Arrange
        var options = WorkerOptions();

        // Act
        var errors = options.Validate(new ValidationContext(options)).ToList();

        // Assert
        Assert.Equal(
            (0, true, "1", "48", "49", "47", "47", "1", "ZZZ", "31", "R-99-PN", "1"),
            (errors.Count, options.Enable, options.PaymentFormCode, options.CardPaymentMeansCodes["CREDIT"], options.CardPaymentMeansCodes["DEBIT"], options.PaymentMeansCodes["PSE"], options.PaymentMeansCodes["NEQUI"], options.UnknownPaymentMeansCode, options.FullyPrepaidPaymentMeansCode, options.IdentificationTypeCodes["31"], options.BuyerTaxResponsibilities.Single(), options.CreditNoteCorrectionConceptCode));
    }

    [Fact]
    public void Options_EnabledWithoutCodes_IsInvalid()
    {
        // Arrange
        var options = new PlatformInvoicingOptions { Enable = true };

        // Act
        var errors = options.Validate(new ValidationContext(options)).ToList();

        // Assert
        Assert.NotEmpty(errors);
    }

    private static PlatformInvoicingOptions WorkerOptions()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !System.IO.File.Exists(Path.Combine(directory.FullName, "CodeDesignPlus.Net.Microservice.Licenses.sln")))
        {
            directory = directory.Parent;
        }

        var path = Path.Combine(directory!.FullName, "src", "entrypoints", "CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker", "appsettings.json");
        var configuration = new ConfigurationBuilder().AddJsonFile(path).Build();
        var options = new PlatformInvoicingOptions();

        configuration.GetSection(PlatformInvoicingOptions.Section).Bind(options);

        return options;
    }

    /// <summary>
    /// The twin of the validation reads the attached document that ms-electronicinvoicing now publishes (pendings/661, step 4).
    /// </summary>
    [Fact]
    public async Task ElectronicDocumentValidatedHandler_JsonWithTheAttachedDocument_PassesItToTheOrder()
    {
        // Arrange
        var attachedId = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new
        {
            AggregateId = Guid.NewGuid(),
            Reference = "license-order:x",
            Prefix = "SETP",
            Consecutive = 990000001L,
            Cufe = "cufe-1",
            ValidatedAt = Instant.FromUtc(2026, 11, 10, 15, 0),
            AttachedDocument = StoredFile.Create(attachedId, "platform-electronic-documents", "SETP990000001-attached.xml", ".xml", "application/xml", 1024)
        });

        var @event = JsonSerializer.Deserialize<ElectronicDocumentValidatedDomainEvent>(json);

        // Act
        await new ElectronicDocumentValidatedHandler(mediator.Object).HandleAsync(@event, CancellationToken.None);

        // Assert
        Assert.Equal(attachedId, Assert.IsType<RecordElectronicDocumentValidatedCommand>(Assert.Single(sent)).AttachedDocument!.Id);
    }

    /// <summary>
    /// The twin of the files kept after the validation reads them as ms-electronicinvoicing writes them.
    /// </summary>
    [Fact]
    public async Task ElectronicDocumentFilesKeptHandler_JsonOfElectronicInvoicing_KeepsTheFiles()
    {
        // Arrange
        var documentId = Guid.NewGuid();
        var xmlId = Guid.NewGuid();
        var json = JsonSerializer.Serialize(new
        {
            AggregateId = documentId,
            Tenant = Guid.Parse("00000000-e1ec-0000-0000-000000000001"),
            Reference = "license-order:x",
            Prefix = "SETP",
            Consecutive = 990000001L,
            Xml = StoredFile.Create(xmlId, "platform-electronic-documents", "SETP990000001.xml", ".xml", "application/xml", 1024),
            AttachedDocument = StoredFile.Create(Guid.NewGuid(), "platform-electronic-documents", "SETP990000001-attached.xml", ".xml", "application/xml", 1024),
            Pdf = StoredFile.Create(Guid.NewGuid(), "platform-electronic-documents", "SETP990000001.pdf", ".pdf", "application/pdf", 1024)
        });

        var @event = JsonSerializer.Deserialize<ElectronicDocumentFilesKeptDomainEvent>(json);

        // Act
        await new ElectronicDocumentFilesKeptHandler(mediator.Object).HandleAsync(@event, CancellationToken.None);

        // Assert
        var command = Assert.IsType<RecordElectronicDocumentFilesCommand>(Assert.Single(sent));

        Assert.Equal((documentId, "license-order:x", xmlId, true, true), (command.DocumentId, command.Reference, command.Xml.Id, command.AttachedDocument is not null, command.Pdf is not null));
    }
}
