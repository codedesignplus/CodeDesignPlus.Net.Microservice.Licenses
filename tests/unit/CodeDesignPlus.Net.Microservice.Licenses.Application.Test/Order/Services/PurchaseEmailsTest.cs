using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.File.Storage.Abstractions;
using CodeDesignPlus.Net.File.Storage.Abstractions.Providers;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.FileStorage;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Xunit;
using MsOptions = Microsoft.Extensions.Options.Options;
using StorageResponse = CodeDesignPlus.Net.File.Storage.Abstractions.Models.Response;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order.Services;

/// <summary>
/// The email of a paid order waits for its electronic invoice to attach it, and never goes twice (pendings/661, step 4).
/// </summary>
public class PurchaseEmailsTest
{
    private static readonly Instant PaidAt = Instant.FromUtc(2026, 11, 10, 15, 0);
    private static readonly Instant WaitsUntil = PaidAt + Duration.FromMinutes(30);
    private static readonly Guid PlatformContainer = Guid.Parse("00000000-f11e-0000-0000-000000000001");

    private static readonly FileAttachment Xml = new(Guid.Parse("a1000000-0000-4000-8000-000000000001"), "SETP991148967.xml", "platform-electronic-documents");
    private static readonly FileAttachment Attached = new(Guid.Parse("a1000000-0000-4000-8000-000000000002"), "SETP991148967-attached.xml", "platform-electronic-documents");
    private static readonly FileAttachment Pdf = new(Guid.Parse("a1000000-0000-4000-8000-000000000003"), "SETP991148967.pdf", "platform-electronic-documents");

    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<IPubSub> pubsub = new();
    private readonly Mock<ICurrencyGrpc> currencies = new();
    private readonly Mock<IFileStorageGrpc> uploads = new();
    private readonly Mock<IFileStorage> storage = new();
    private readonly List<SendEmailDomainEvent> sent = [];
    private readonly List<UploadFileRequest> saved = [];

    public PurchaseEmailsTest()
    {
        currencies.Setup(x => x.GetCurrencyAsync(null, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>())).ReturnsAsync(TestOrders.Cop);
        uploads
            .Setup(x => x.UploadAsync(It.IsAny<UploadFileRequest>(), It.IsAny<CancellationToken>()))
            .Callback<UploadFileRequest, CancellationToken>((request, _) => saved.Add(request))
            .ReturnsAsync((UploadFileRequest request, CancellationToken _) => new UploadFileResponse { Id = request.Id, Target = request.Target, FileName = request.FileName });
        storage
            .Setup(x => x.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), PlatformContainer, It.IsAny<CancellationToken>()))
            .ReturnsAsync((string file, string _, Guid _, CancellationToken _) => new StorageResponse(new CodeDesignPlus.Net.File.Storage.Abstractions.Models.File(file), TypeProviders.AzureBlobProvider) { Success = true, Stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes($"content of {file}")) });
        pubsub
            .Setup(x => x.PublishAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IDomainEvent, CancellationToken>((@event, _) => sent.Add((SendEmailDomainEvent)@event))
            .Returns(Task.CompletedTask);
        orders.Setup(x => x.ClaimReceiptEmailAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        orders.Setup(x => x.ClaimInvoiceEmailAsync(It.IsAny<Guid>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    private static PlatformInvoicingOptions Options(bool enable = true)
    {
        var options = PlatformInvoiceTest.Options();

        options.Enable = enable;
        options.EmailWaitForInvoice = TimeSpan.FromMinutes(30);
        options.FilesContainer = PlatformContainer;

        return options;
    }

    private PurchaseEmails Emails(Instant now, bool enable = true) => new(orders.Object, pubsub.Object, currencies.Object, uploads.Object, storage.Object, MsOptions.Create(Options(enable)), new FixedClock(now), Mock.Of<ILogger<PurchaseEmails>>());

    /// <summary>
    /// A purchase paid at <see cref="PaidAt"/> with its receipt, whose email waits for the invoice, and its invoice as given.
    /// </summary>
    private OrderAggregate Stored(OrderElectronicDocument? invoice = null, bool waits = true)
    {
        var order = PlatformInvoiceTest.PaidPurchase();

        order.AttachReceipt(Guid.Parse("b1000000-0000-4000-8000-000000000001"), "ComprobanteCompra.pdf", "licenses-pdf", TestOrders.BuyerId);

        if (waits)
        {
            order.WaitForInvoiceBeforeEmail(WaitsUntil);
        }

        order.RequestElectronicDocuments(invoice ?? Requested(order), []);
        orders.Setup(x => x.FindAsync<OrderAggregate>(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        return order;
    }

    private static OrderElectronicDocument Requested(OrderAggregate order) => OrderElectronicDocument.Requested(PlatformInvoice.InvoiceIdOf(order.Id), PlatformInvoice.InvoiceReferenceOf(PlatformInvoiceTest.Options(), order.Id), null, PaidAt);

    private static OrderElectronicDocument Validated(OrderAggregate order, FileAttachment? attached = null) => Requested(order).Validate("SETP", 991148967, "cufe-1", new LocalDate(2026, 11, 10), PaidAt, Xml, Pdf, attached ?? Attached);

    private OrderAggregate StoredWithValidatedInvoice()
    {
        var order = Stored();

        order.RequestElectronicDocuments(Validated(order), []);

        return order;
    }

    [Fact]
    public void WaitsUntil_InvoicedOrder_WaitsTheConfiguredTime()
    {
        // Arrange
        var order = PlatformInvoiceTest.PaidPurchase();

        // Act
        var until = Emails(PaidAt).WaitsUntil(order);

        // Assert
        Assert.Equal(WaitsUntil, until);
    }

    [Fact]
    public void WaitsUntil_InvoicingOff_DoesNotWait()
    {
        // Act
        var until = Emails(PaidAt, enable: false).WaitsUntil(PlatformInvoiceTest.PaidPurchase());

        // Assert
        Assert.Null(until);
    }

    [Fact]
    public void Due_OrderThatDoesNotWait_IsNothingEvenWithItsInvoice()
    {
        // Arrange: paid before the wait existed, its receipt already went out when the payment was approved.
        var order = Stored(waits: false);

        order.RequestElectronicDocuments(Validated(order), []);

        // Act
        var due = PurchaseEmailDue.Of(order, WaitsUntil + Duration.FromHours(1));

        // Assert
        Assert.Equal(PurchaseEmailDue.Nothing, due);
    }

    [Fact]
    public void Due_InvoiceNotValidatedWithinTheWait_IsNothing()
    {
        // Act
        var due = PurchaseEmailDue.Of(Stored(), WaitsUntil - Duration.FromMinutes(1));

        // Assert
        Assert.Equal(PurchaseEmailDue.Nothing, due);
    }

    [Fact]
    public void Due_InvoiceNotValidatedAfterTheWait_IsTheReceiptAlone()
    {
        // Act
        var due = PurchaseEmailDue.Of(Stored(), WaitsUntil);

        // Assert
        Assert.Equal(new PurchaseEmailDue(true, false), due);
    }

    [Fact]
    public void Due_InvoiceValidatedWithItsFiles_IsTheReceiptWithTheInvoice()
    {
        // Act
        var due = PurchaseEmailDue.Of(StoredWithValidatedInvoice(), PaidAt + Duration.FromMinutes(2));

        // Assert
        Assert.Equal(new PurchaseEmailDue(true, true), due);
    }

    [Fact]
    public void Due_InvoiceValidatedWithoutItsAttachedDocument_KeepsWaiting()
    {
        // Arrange
        var order = Stored();

        order.RequestElectronicDocuments(Requested(order).Validate("SETP", 991148967, "cufe-1", null, PaidAt, Xml, Pdf), []);

        // Act
        var due = PurchaseEmailDue.Of(order, PaidAt + Duration.FromMinutes(2));

        // Assert
        Assert.Equal(PurchaseEmailDue.Nothing, due);
    }

    [Fact]
    public void Due_InvoiceRejected_IsTheReceiptAloneWithoutWaiting()
    {
        // Arrange
        var order = Stored();

        order.RequestElectronicDocuments(Requested(order).Reject("SETP", 991148967, ["FAK24"], PaidAt), []);

        // Act
        var due = PurchaseEmailDue.Of(order, PaidAt + Duration.FromMinutes(2));

        // Assert
        Assert.Equal(new PurchaseEmailDue(true, false), due);
    }

    [Fact]
    public void Due_InvoiceValidatedAfterTheReceiptWent_IsTheInvoiceAlone()
    {
        // Arrange
        var order = StoredWithValidatedInvoice();

        order.MarkReceiptEmailSent(WaitsUntil, withInvoice: false);

        // Act
        var due = PurchaseEmailDue.Of(order, WaitsUntil + Duration.FromHours(1));

        // Assert
        Assert.Equal(new PurchaseEmailDue(false, true), due);
    }

    [Fact]
    public void Due_ReceiptAndInvoiceSent_IsNothing()
    {
        // Arrange
        var order = StoredWithValidatedInvoice();

        order.MarkReceiptEmailSent(PaidAt, withInvoice: true);

        // Act
        var due = PurchaseEmailDue.Of(order, WaitsUntil + Duration.FromHours(1));

        // Assert
        Assert.Equal(PurchaseEmailDue.Nothing, due);
    }

    [Fact]
    public void Due_ChangeWithItsCreditNoteValidatedButNotItsInvoice_KeepsWaiting()
    {
        // Arrange: the note goes first; what the buyer waits for is the invoice.
        var order = Stored();
        var previous = Guid.NewGuid();
        var note = OrderElectronicDocument.Requested(PlatformInvoice.CreditNoteIdOf(order.Id, previous), "license-order:x:credit-note:y", previous, PaidAt).Validate("NC", 1, "cude", null, PaidAt, Xml, Pdf, Attached);

        order.RequestElectronicDocuments(Requested(order), [note]);

        // Act
        var due = PurchaseEmailDue.Of(order, PaidAt + Duration.FromMinutes(2));

        // Assert
        Assert.Equal(PurchaseEmailDue.Nothing, due);
    }

    [Fact]
    public async Task SendDueAsync_InvoiceValidated_SendsTheReceiptTheXmlAndTheZipInOneEmail()
    {
        // Arrange
        var order = StoredWithValidatedInvoice();

        // Act
        await Emails(PaidAt + Duration.FromMinutes(2)).SendDueAsync(order.Id, CancellationToken.None);

        // Assert
        var email = Assert.Single(sent);

        Assert.Equal(
            ("PurchaseConfirmation", 3, "ComprobanteCompra.pdf", "SETP991148967.xml", "SETP991148967.zip", true),
            (email.TemplateName, email.Attachments.Count, email.Attachments[0].Name, email.Attachments[1].Name, email.Attachments[2].Name, email.Variables[ElectronicInvoiceText.NoteVariable].Contains("SETP991148967", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task SendDueAsync_InvoiceValidated_ReservesTheReceiptAndTheInvoice()
    {
        // Arrange
        var order = StoredWithValidatedInvoice();
        var now = PaidAt + Duration.FromMinutes(2);

        // Act
        await Emails(now).SendDueAsync(order.Id, CancellationToken.None);

        // Assert
        orders.Verify(x => x.ClaimReceiptEmailAsync(order.Id, true, now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendDueAsync_InvoiceValidated_ZipCarriesTheAttachedDocumentAndThePdf()
    {
        // Arrange
        var order = StoredWithValidatedInvoice();

        // Act
        await Emails(PaidAt + Duration.FromMinutes(2)).SendDueAsync(order.Id, CancellationToken.None);

        // Assert
        var zip = saved.Single(x => x.FileName.EndsWith(".zip", StringComparison.Ordinal));

        using var archive = new ZipArchive(new MemoryStream(zip.Content.ToByteArray()), ZipArchiveMode.Read);

        var entries = archive.Entries.ToDictionary(x => x.FullName, x => new StreamReader(x.Open()).ReadToEnd());

        Assert.Equal(
            ("content of SETP991148967-attached.xml", "content of SETP991148967.pdf", 2),
            (entries["adSETP991148967.xml"], entries["SETP991148967.pdf"], entries.Count));
    }

    [Fact]
    public async Task SendDueAsync_InvoiceValidated_SavesTheAttachmentsInTheFolderOfTheCopropiedadWithIdsOfTheOrder()
    {
        // Arrange
        var order = StoredWithValidatedInvoice();

        // Act
        await Emails(PaidAt + Duration.FromMinutes(2)).SendDueAsync(order.Id, CancellationToken.None);
        var first = saved.Select(x => x.Id).ToList();

        await Emails(PaidAt + Duration.FromMinutes(3)).SendDueAsync(order.Id, CancellationToken.None);

        // Assert: a retry saves on the same records.
        Assert.Equal(
            (true, true, string.Join(",", first)),
            (saved.All(x => x.Target == "licenses-pdf"), saved.All(x => x.Tenant == order.TenantDetail.Id.ToString()), string.Join(",", saved.Skip(2).Select(x => x.Id))));
    }

    [Fact]
    public async Task SendDueAsync_AnotherProcessSentIt_SendsNothing()
    {
        // Arrange
        var order = StoredWithValidatedInvoice();

        orders.Setup(x => x.ClaimReceiptEmailAsync(order.Id, It.IsAny<bool>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);

        // Act
        await Emails(PaidAt + Duration.FromMinutes(2)).SendDueAsync(order.Id, CancellationToken.None);

        // Assert
        Assert.Empty(sent);
    }

    [Fact]
    public async Task SendDueAsync_PublicationFails_GivesBackTheReservation()
    {
        // Arrange
        var order = StoredWithValidatedInvoice();
        var now = PaidAt + Duration.FromMinutes(2);

        pubsub.Setup(x => x.PublishAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("bus down"));

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => Emails(now).SendDueAsync(order.Id, CancellationToken.None));

        // Assert
        orders.Verify(x => x.ReleaseReceiptEmailAsync(order.Id, true, now, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendDueAsync_InvoiceAfterTheReceipt_SendsItAloneWithItsTemplate()
    {
        // Arrange
        var order = StoredWithValidatedInvoice();
        var now = WaitsUntil + Duration.FromHours(1);

        order.MarkReceiptEmailSent(WaitsUntil, withInvoice: false);

        // Act
        await Emails(now).SendDueAsync(order.Id, CancellationToken.None);

        // Assert
        var email = Assert.Single(sent);

        orders.Verify(x => x.ClaimInvoiceEmailAsync(order.Id, now, It.IsAny<CancellationToken>()), Times.Once);

        Assert.Equal(
            (ElectronicInvoiceText.InvoiceTemplate, "SETP991148967", "SETP991148967.xml,SETP991148967.zip"),
            (email.TemplateName, email.Variables["invoice_number"], string.Join(",", email.Attachments.Select(x => x.Name))));
    }

    [Fact]
    public async Task SendDueAsync_WaitIsOver_SendsTheReceiptAloneWithoutTheNote()
    {
        // Arrange
        var order = Stored();

        // Act
        await Emails(WaitsUntil).SendDueAsync(order.Id, CancellationToken.None);

        // Assert
        var email = Assert.Single(sent);

        orders.Verify(x => x.ClaimReceiptEmailAsync(order.Id, false, WaitsUntil, It.IsAny<CancellationToken>()), Times.Once);

        Assert.Equal((1, string.Empty), (email.Attachments.Count, email.Variables[ElectronicInvoiceText.NoteVariable]));
    }

    [Fact]
    public async Task SendDueAsync_FilesOfTheInvoiceUnreadableAfterTheWait_SendsTheReceiptAlone()
    {
        // Arrange
        var order = StoredWithValidatedInvoice();

        storage.Setup(x => x.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((StorageResponse)null!);

        // Act
        await Emails(WaitsUntil).SendDueAsync(order.Id, CancellationToken.None);

        // Assert
        orders.Verify(x => x.ClaimReceiptEmailAsync(order.Id, false, WaitsUntil, It.IsAny<CancellationToken>()), Times.Once);

        Assert.Single(Assert.Single(sent).Attachments);
    }

    [Fact]
    public async Task SendDueAsync_FilesOfTheInvoiceUnreadableWithinTheWait_FailsWithoutReservingAnything()
    {
        // Arrange
        var order = StoredWithValidatedInvoice();

        storage.Setup(x => x.DownloadAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync((StorageResponse)null!);

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => Emails(PaidAt + Duration.FromMinutes(2)).SendDueAsync(order.Id, CancellationToken.None));

        // Assert
        orders.Verify(x => x.ClaimReceiptEmailAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SendDueAsync_InvoiceFiles_AreReadFromThePlatformContainer()
    {
        // Arrange
        var order = StoredWithValidatedInvoice();

        // Act
        await Emails(PaidAt + Duration.FromMinutes(2)).SendDueAsync(order.Id, CancellationToken.None);

        // Assert
        storage.Verify(x => x.DownloadAsync(Attached.Name, $"{Attached.Target}/{Attached.Id}", PlatformContainer, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SendReceiptAsync_OrderNotInvoiced_SendsTheReceiptAsBefore()
    {
        // Arrange
        var order = Stored(waits: false);

        // Act
        await Emails(PaidAt, enable: false).SendReceiptAsync(order, CancellationToken.None);

        // Assert
        var email = Assert.Single(sent);

        Assert.Equal(("PurchaseConfirmation", "ComprobanteCompra.pdf", string.Empty), (email.TemplateName, Assert.Single(email.Attachments).Name, email.Variables[ElectronicInvoiceText.NoteVariable]));
    }

    [Fact]
    public void Options_EnabledWithoutFilesContainer_SaysSo()
    {
        // Arrange
        var options = Options();

        options.FilesContainer = Guid.Empty;

        // Act
        var problems = options.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(options)).ToList();

        // Assert
        Assert.Contains(problems, x => x.MemberNames.Contains(nameof(PlatformInvoicingOptions.FilesContainer)));
    }

    [Fact]
    public void Options_EnabledWithoutWait_SaysSo()
    {
        // Arrange
        var options = Options();

        options.EmailWaitForInvoice = TimeSpan.Zero;

        // Act
        var problems = options.Validate(new System.ComponentModel.DataAnnotations.ValidationContext(options)).ToList();

        // Assert
        Assert.Contains(problems, x => x.MemberNames.Contains(nameof(PlatformInvoicingOptions.EmailWaitForInvoice)));
    }

    private sealed class FixedClock(Instant now) : IClock
    {
        public Instant GetCurrentInstant() => now;
    }
}
