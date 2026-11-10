using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.Notification;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.RecordElectronicDocument;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order.Commands.RecordElectronicDocument;

/// <summary>
/// The order keeps what ms-electronicinvoicing said of its documents, found by their reference and id (pendings/661).
/// </summary>
public class RecordElectronicDocumentCommandHandlersTest
{
    private static readonly Guid Administrators = Guid.Parse("20000000-0000-0000-0000-000000000010");
    private static readonly Instant At = Instant.FromUtc(2026, 11, 10, 16, 0);

    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<IInboxGrpc> inbox = new();
    private readonly List<OrderElectronicDocument> recorded = [];
    private readonly List<NotificationRequest> notifications = [];
    private readonly OrderAggregate order = PlatformInvoiceTest.PaidPurchase();

    public RecordElectronicDocumentCommandHandlersTest()
    {
        order.RequestElectronicDocuments(OrderElectronicDocument.Requested(PlatformInvoice.InvoiceIdOf(order.Id), PlatformInvoice.InvoiceReferenceOf(PlatformInvoiceTest.Options(), order.Id), null, At), []);

        orders.Setup(x => x.FindAsync<OrderAggregate>(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        orders
            .Setup(x => x.RecordElectronicDocumentAsync(order.Id, It.IsAny<OrderElectronicDocument>(), It.IsAny<CancellationToken>()))
            .Callback<Guid, OrderElectronicDocument, CancellationToken>((_, document, _) => recorded.Add(document))
            .ReturnsAsync(true);
        inbox
            .Setup(x => x.NotifyAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequest, CancellationToken>((request, _) => notifications.Add(request))
            .Returns(Task.CompletedTask);
    }

    private Guid InvoiceId => PlatformInvoice.InvoiceIdOf(order.Id);

    private string Reference => PlatformInvoice.InvoiceReferenceOf(PlatformInvoiceTest.Options(), order.Id);

    private RecordElectronicDocumentValidatedCommandHandler Validated() => new(orders.Object, MsOptions.Create(PlatformInvoiceTest.Options()), Mock.Of<ILogger<RecordElectronicDocumentValidatedCommandHandler>>());

    private RecordElectronicDocumentRejectedCommandHandler Rejected(Guid? role = null) => new(orders.Object, inbox.Object, MsOptions.Create(PlatformInvoiceTest.Options()), MsOptions.Create(new PlatformNoticeOptions { PlatformAdministratorRole = role ?? Administrators }), Mock.Of<ILogger<RecordElectronicDocumentRejectedCommandHandler>>());

    private RecordElectronicDocumentRejectedCommand Rejection(Guid? eventId = null) => new(InvoiceId, Reference, "SETP", 990000002, true, ["FAK24: el NIT del adquiriente no existe"], At, eventId ?? Guid.NewGuid());

    [Fact]
    public async Task Handle_InvoiceValidated_KeepsItsNumberCufeAndFiles()
    {
        // Arrange
        var pdf = new FileAttachment(Guid.NewGuid(), "SETP990000001.pdf", "electronic-invoicing");

        // Act
        await Validated().Handle(new RecordElectronicDocumentValidatedCommand(InvoiceId, Reference, "SETP", 990000001, "cufe-1", new LocalDate(2026, 11, 10), At, null, pdf), CancellationToken.None);

        // Assert
        var invoice = Assert.Single(recorded);

        Assert.Equal((ElectronicDocumentState.Validated, "SETP990000001", "cufe-1", pdf.Id), (invoice.State, invoice.Number, invoice.Cufe, invoice.Pdf!.Id));
    }

    /// <summary>
    /// ms-electronicinvoicing publishes every issuer's documents on the same topic: a document whose reference is not of
    /// an order of this service is someone else's and is not even looked up.
    /// </summary>
    [Fact]
    public async Task Handle_DocumentOfAnotherCaller_IsIgnored()
    {
        // Act
        await Validated().Handle(new RecordElectronicDocumentValidatedCommand(InvoiceId, $"invoicing:{order.Id}", "FE", 1, "cufe", null, At, null, null), CancellationToken.None);

        // Assert
        orders.Verify(x => x.FindAsync<OrderAggregate>(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// A document id that is not of the order (a stale reference) changes nothing.
    /// </summary>
    [Fact]
    public async Task Handle_UnknownDocumentOfTheOrder_ChangesNothing()
    {
        // Act
        await Validated().Handle(new RecordElectronicDocumentValidatedCommand(Guid.NewGuid(), Reference, "SETP", 1, "cufe", null, At, null, null), CancellationToken.None);

        // Assert
        Assert.Empty(recorded);
    }

    [Fact]
    public async Task Handle_InvoiceRejected_KeepsWhy()
    {
        // Act
        await Rejected().Handle(Rejection(), CancellationToken.None);

        // Assert
        var invoice = Assert.Single(recorded);

        Assert.Equal((ElectronicDocumentState.Rejected, "FAK24: el NIT del adquiriente no existe"), (invoice.State, Assert.Single(invoice.Reasons)));
    }

    /// <summary>
    /// Whoever has to correct it hears about it without going to look (rule 55).
    /// </summary>
    [Fact]
    public async Task Handle_InvoiceRejected_TellsThePlatformAdministrator()
    {
        // Act
        await Rejected().Handle(Rejection(), CancellationToken.None);

        // Assert
        var notification = Assert.Single(notifications);

        Assert.Equal(
            (PlatformNotificationKinds.LicenseInvoiceRejected, Administrators.ToString(), "La DIAN rechazó la factura electrónica de la orden de Conjunto Las Acacias: FAK24: el NIT del adquiriente no existe. Corrígela y vuelve a enviarla."),
            (notification.Kind, Assert.Single(notification.Audience.Values), notification.Body));
    }

    /// <summary>
    /// A rejection that arrives after the validation does not undo it, and tells nobody.
    /// </summary>
    [Fact]
    public async Task Handle_RejectionOfAValidatedDocument_TellsNobody()
    {
        // Arrange
        orders.Setup(x => x.RecordElectronicDocumentAsync(order.Id, It.IsAny<OrderElectronicDocument>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        await Rejected().Handle(Rejection(), CancellationToken.None);

        // Assert
        Assert.Empty(notifications);
    }

    /// <summary>
    /// A redelivery of the same rejection notifies with the same id, so the inbox keeps one.
    /// </summary>
    [Fact]
    public async Task Handle_SameRejectionTwice_NotifiesWithTheSameId()
    {
        // Arrange
        var command = Rejection(Guid.NewGuid());

        // Act
        await Rejected().Handle(command, CancellationToken.None);
        await Rejected().Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(notifications[0].Id, notifications[1].Id);
    }

    [Fact]
    public async Task Handle_RejectedWithoutTheRole_FailsSoTheMessageIsRetried()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => Rejected(Guid.Empty).Handle(Rejection(), CancellationToken.None));
    }
}
