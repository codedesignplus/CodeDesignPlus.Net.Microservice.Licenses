using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using CodeDesignPlus.Net.ValueObjects.Location;
using CodeDesignPlus.Net.ValueObjects.User;
using License = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.License;
using LicenseModule = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.LicenseModule;
using Tenant = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.Tenant;

namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Test.Repositories;

/// <summary>
/// The writes of the electronic documents of an order are partial and conditional (pendings/661): the order is written by
/// the provisioning at the same time, and a redelivery or a late answer must not undo what is already there.
/// </summary>
public class ElectronicDocumentWritesTest
{
    private static readonly Guid OrderId = Guid.Parse("aa000000-0000-4000-8000-000000000301");
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 10, 15, 0);

    public ElectronicDocumentWritesTest()
    {
        CodeDesignPlus.Net.Mongo.Extensions.MongoSerializerRegistration.RegisterSerializers();
    }

    private static BsonDocument Render(FilterDefinition<OrderAggregate> filter) =>
        filter.Render(new RenderArgs<OrderAggregate>(BsonSerializer.SerializerRegistry.GetSerializer<OrderAggregate>(), BsonSerializer.SerializerRegistry));

    private static BsonDocument Render(UpdateDefinition<OrderAggregate> update) =>
        update.Render(new RenderArgs<OrderAggregate>(BsonSerializer.SerializerRegistry.GetSerializer<OrderAggregate>(), BsonSerializer.SerializerRegistry)).AsBsonDocument;

    private static OrderAggregate PaidWithACreditCard()
    {
        var currency = Currency.Create(Guid.NewGuid(), "Peso Colombiano", "COP", "$", 2, 170);
        var country = Country.Create(Guid.NewGuid(), "Colombia", "CO", "COL", 170, "+57", "America/Bogota", currency);
        var location = Location.Create(country, State.Create(Guid.NewGuid(), "Bogotá, D.C.", "11"), City.Create(Guid.NewGuid(), "Bogotá, D.C.", "America/Bogota"), null, null, "Carrera 7 # 71-21", "110111");
        var tenant = Tenant.Create(Guid.NewGuid(), "Conjunto Las Acacias", null, TypeDocument.Create("NIT", "NIT"), "901888777-3", "+573001234567", "admin@acacias.com", location);
        var license = License.Create(Guid.NewGuid(), "Esencial", Money.FromLong(14_280_000L, "COP"), Money.FromLong(2_280_000L, "COP"), Money.FromLong(12_000_000L, "COP"), BillingType.Monthly, BillingModel.None, "Plan", "Plan", Icon.Create("solar:home-2-linear", "#2563eb"), "Terms", false, true, [], [new LicenseModule(Guid.NewGuid(), "Organización", "Identidad")]);
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid(), license, Buyer.CreateMinimal(Guid.NewGuid(), "Buyer", "+573001234567", "buyer@example.com"), tenant, Guid.NewGuid());

        order.SetPaymentStatus(PaymentStatus.Succeeded, order.Buyer.BuyerId, paymentMethodType: "CARD", cardType: "CREDIT");

        return order;
    }

    /// <summary>
    /// The result of the payment writes with what the payer paid, which the invoice needs; a write without it would leave
    /// every invoice with the instrument not defined.
    /// </summary>
    [Fact]
    public void PaymentStatusUpdate_WritesTheInstrumentAndTheCardType()
    {
        // Act
        var set = Render(OrderRepository.PaymentStatusUpdate(PaidWithACreditCard()))["$set"].AsBsonDocument;

        // Assert
        Assert.Equal(("CARD", "CREDIT"), (set["PaymentMethodType"].AsString, set["CardType"].AsString));
    }

    /// <summary>
    /// The request is written only on an order without invoice: a redelivery cannot turn a validated invoice back into a
    /// requested one.
    /// </summary>
    [Fact]
    public void RequestElectronicDocumentsFilter_OnlyAnOrderWithoutInvoice()
    {
        // Act
        var filter = Render(OrderRepository.RequestElectronicDocumentsFilter(OrderId));

        // Assert
        Assert.Equal(BsonNull.Value, filter["Invoice"]);
    }

    /// <summary>
    /// The answer for the invoice is written only while the invoice is that document and is not validated: a late
    /// rejection does not undo a validation.
    /// </summary>
    [Fact]
    public void RecordElectronicDocumentWrite_Invoice_OnlyWhileItIsNotValidated()
    {
        // Arrange
        var invoice = OrderElectronicDocument.Requested(Guid.NewGuid(), "license-order:x", null, Now).Reject(null, null, ["FAK24"], Now);

        // Act
        var (filter, update) = OrderRepository.RecordElectronicDocumentWrite(OrderId, invoice);
        var rendered = Render(filter);

        // Assert
        Assert.Equal(
            (new BsonDocument("$ne", (int)ElectronicDocumentState.Validated), (BsonValue)new BsonBinaryData(invoice.DocumentId, GuidRepresentation.Standard), true),
            (rendered["Invoice.State"].AsBsonDocument, rendered["Invoice.DocumentId"], Render(update)["$set"].AsBsonDocument.Contains("Invoice")));
    }

    /// <summary>
    /// A credit note is written in its place of the array, matched by its id and only while it is not validated, so the
    /// other notes of the order are not touched.
    /// </summary>
    [Fact]
    public void RecordElectronicDocumentWrite_CreditNote_OnlyThatNoteInItsPlace()
    {
        // Arrange
        var note = OrderElectronicDocument.Requested(Guid.NewGuid(), "license-order:x:credit-note:y", Guid.NewGuid(), Now);

        // Act
        var (filter, update) = OrderRepository.RecordElectronicDocumentWrite(OrderId, note);
        var match = Render(filter)["CreditNotes"]["$elemMatch"].AsBsonDocument;

        // Assert
        Assert.Equal(
            (new BsonDocument("$ne", (int)ElectronicDocumentState.Validated), true),
            (match["State"].AsBsonDocument, Render(update)["$set"].AsBsonDocument.Contains("CreditNotes.$")));
    }

    /// <summary>
    /// The email of the receipt is reserved only while it has not been sent: of the consumer of the invoice and the job, one
    /// sends it (pendings/661, step 4).
    /// </summary>
    [Fact]
    public void ClaimReceiptEmailWrite_Alone_OnlyWhileTheReceiptWasNotSent()
    {
        // Act
        var (filter, update) = OrderRepository.ClaimReceiptEmailWrite(OrderId, false, Now);
        var rendered = Render(filter);
        var set = Render(update)["$set"].AsBsonDocument;

        // Assert
        Assert.Equal((BsonNull.Value, false, true, false), (rendered["ReceiptEmailSentAt"], rendered.Contains("InvoiceEmailSentAt"), set.Contains("ReceiptEmailSentAt"), set.Contains("InvoiceEmailSentAt")));
    }

    /// <summary>
    /// With the invoice, it is reserved only while neither went, and marks both.
    /// </summary>
    [Fact]
    public void ClaimReceiptEmailWrite_WithTheInvoice_OnlyWhileNeitherWasSent()
    {
        // Act
        var (filter, update) = OrderRepository.ClaimReceiptEmailWrite(OrderId, true, Now);
        var rendered = Render(filter);
        var set = Render(update)["$set"].AsBsonDocument;

        // Assert
        Assert.Equal((BsonNull.Value, BsonNull.Value, true, true), (rendered["ReceiptEmailSentAt"], rendered["InvoiceEmailSentAt"], set.Contains("ReceiptEmailSentAt"), set.Contains("InvoiceEmailSentAt")));
    }

    /// <summary>
    /// The invoice alone goes only after the receipt and while the invoice has not been sent.
    /// </summary>
    [Fact]
    public void ClaimInvoiceEmailWrite_OnlyAfterTheReceiptAndWhileTheInvoiceWasNotSent()
    {
        // Act
        var (filter, _) = OrderRepository.ClaimInvoiceEmailWrite(OrderId, Now);
        var rendered = Render(filter);

        // Assert
        Assert.Equal((new BsonDocument("$ne", BsonNull.Value), (BsonValue)BsonNull.Value), (rendered["ReceiptEmailSentAt"].AsBsonDocument, rendered["InvoiceEmailSentAt"]));
    }

    /// <summary>
    /// A reservation is given back only by whoever made it: the one of the same instant.
    /// </summary>
    [Fact]
    public void ReleaseReceiptEmailWrite_OnlyTheReservationOfThatInstant()
    {
        // Act
        var (filter, update) = OrderRepository.ReleaseReceiptEmailWrite(OrderId, true, Now);
        var rendered = Render(filter);
        var set = Render(update)["$set"].AsBsonDocument;

        // Assert
        Assert.Equal((false, false, BsonNull.Value, BsonNull.Value), (rendered["ReceiptEmailSentAt"].IsBsonNull, rendered["InvoiceEmailSentAt"].IsBsonNull, set["ReceiptEmailSentAt"], set["InvoiceEmailSentAt"]));
    }

    /// <summary>
    /// The job takes only the orders that wait, whose wait is over and whose email has not gone: never one paid before the
    /// wait existed.
    /// </summary>
    [Fact]
    public void ReceiptEmailsOverdueFilter_OnlyThoseThatWaitPastTheirTimeAndWereNotSent()
    {
        // Act
        var rendered = Render(OrderRepository.ReceiptEmailsOverdueFilter(Now));
        var text = rendered.ToString();

        // Assert
        Assert.Equal((true, true, true), (text.Contains("\"$ne\" : null", StringComparison.Ordinal), text.Contains("$lte", StringComparison.Ordinal), text.Contains("\"ReceiptEmailSentAt\" : null", StringComparison.Ordinal)));
    }

    /// <summary>
    /// The files kept after the validation are written only on that validated document.
    /// </summary>
    [Fact]
    public void RecordElectronicDocumentFilesWrite_Invoice_OnlyWhileItIsValidated()
    {
        // Arrange
        var invoice = OrderElectronicDocument.Requested(Guid.NewGuid(), "license-order:x", null, Now).Validate("SETP", 1, "cufe", null, Now, null, null);

        // Act
        var (filter, _) = OrderRepository.RecordElectronicDocumentFilesWrite(OrderId, invoice);

        // Assert
        Assert.Equal((BsonValue)(int)ElectronicDocumentState.Validated, Render(filter)["Invoice.State"]);
    }
}
