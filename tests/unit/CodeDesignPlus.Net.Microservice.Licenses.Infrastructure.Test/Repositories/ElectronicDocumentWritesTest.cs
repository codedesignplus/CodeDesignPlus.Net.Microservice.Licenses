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
}
