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
            (errors.Count, options.Enable, options.PaymentFormCode, options.CardPaymentMeansCodes["CREDIT"], options.CardPaymentMeansCodes["DEBIT"], options.PaymentMeansCodes["PSE"], options.PaymentMeansCodes["NEQUI"], options.UnknownPaymentMeansCode, options.FullyPrepaidPaymentMeansCode, options.IdentificationTypeCodes["NIT"], options.BuyerTaxResponsibilities.Single(), options.CreditNoteCorrectionConceptCode));
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
}
