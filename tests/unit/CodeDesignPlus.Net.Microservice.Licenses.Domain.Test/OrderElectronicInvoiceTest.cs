using CodeDesignPlus.Net.Exceptions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.Mongo.Extensions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test;

/// <summary>
/// What the order keeps for its electronic invoice (pendings/661).
/// </summary>
public class OrderElectronicInvoiceTest
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 10, 15, 0);

    public OrderElectronicInvoiceTest()
    {
        MongoSerializerRegistration.RegisterSerializers();
    }

    private static OrderElectronicDocument Invoice(Guid orderId) => OrderElectronicDocument.Requested(Guid.NewGuid(), $"license-order:{orderId}", null, Now);

    /// <summary>
    /// The approval keeps with what the payer paid: the invoice translates it to the tax authority's payment means.
    /// </summary>
    [Fact]
    public void SetPaymentStatus_ApprovedWithACard_KeepsTheInstrumentAndTheCardType()
    {
        // Arrange
        var order = OrdenDePrueba.Nueva();

        // Act
        order.SetPaymentStatus(PaymentStatus.Succeeded, OrdenDePrueba.BuyerId, paymentMethodType: " CARD ", cardType: "DEBIT");

        // Assert
        Assert.Equal(("CARD", "DEBIT"), (order.PaymentMethodType, order.CardType));
    }

    [Fact]
    public void SetPaymentStatus_Declined_KeepsNoInstrument()
    {
        // Arrange
        var order = OrdenDePrueba.Nueva();

        // Act
        order.SetPaymentStatus(PaymentStatus.Failed, OrdenDePrueba.BuyerId, paymentMethodType: "CARD", cardType: "CREDIT");

        // Assert
        Assert.Equal(((string?)null, (string?)null), (order.PaymentMethodType, order.CardType));
    }

    [Fact]
    public void SetPaymentStatus_Approved_KnowsWhenItWasPaid()
    {
        // Arrange
        var order = OrdenDePrueba.Nueva();

        // Act
        order.SetPaymentStatus(PaymentStatus.Succeeded, OrdenDePrueba.BuyerId);

        // Assert
        Assert.NotNull(order.PaidAt);
    }

    [Fact]
    public void RequestElectronicDocuments_UnpaidOrder_IsRefused()
    {
        // Arrange
        var order = OrdenDePrueba.Nueva();

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => order.RequestElectronicDocuments(Invoice(order.Id), []));

        // Assert
        Assert.Equal(Errors.OnlyAPaidOrderIsInvoiced.Code, exception.Code);
    }

    [Fact]
    public void ElectronicDocumentOf_IdOfACreditNote_FindsTheNote()
    {
        // Arrange
        var order = OrdenDePrueba.Nueva();
        var note = OrderElectronicDocument.Requested(Guid.NewGuid(), "license-order:x:credit-note:y", Guid.NewGuid(), Now);

        order.SetPaymentStatus(PaymentStatus.Succeeded, OrdenDePrueba.BuyerId);
        order.RequestElectronicDocuments(Invoice(order.Id), [note]);

        // Act
        var found = order.ElectronicDocumentOf(note.DocumentId);

        // Assert
        Assert.Same(note, found);
    }

    /// <summary>
    /// A rejection keeps the number the document already had and says why.
    /// </summary>
    [Fact]
    public void Reject_RequestedDocument_KeepsWhy()
    {
        // Arrange
        var invoice = Invoice(Guid.NewGuid());

        // Act
        var rejected = invoice.Reject("SETP", 7, ["FAK24"], Now);

        // Assert
        Assert.Equal((ElectronicDocumentState.Rejected, "SETP7", "FAK24"), (rejected.State, rejected.Number, Assert.Single(rejected.Reasons)));
    }

    /// <summary>
    /// The orders stored before pendings/661 do not carry the members it added: they read as absent (rule 31).
    /// </summary>
    [Fact]
    public void Deserialize_OrderStoredBeforeTheInvoice_ReadsTheNewMembersAsAbsent()
    {
        // Arrange
        var order = OrdenDePrueba.Nueva();

        order.SetPaymentStatus(PaymentStatus.Succeeded, OrdenDePrueba.BuyerId, paymentMethodType: "CARD", cardType: "CREDIT");
        order.RequestElectronicDocuments(Invoice(order.Id), [OrderElectronicDocument.ForReview(Guid.NewGuid(), "r", Guid.NewGuid(), "sin factura", Now)]);

        var document = order.ToBsonDocument();

        foreach (var member in new[] { "PaymentMethodType", "CardType", "Invoice", "CreditNotes" })
        {
            document.Remove(member);
        }

        // Act
        var read = BsonSerializer.Deserialize<OrderAggregate>(document);

        // Assert
        Assert.Equal(((string?)null, (string?)null, false, false), (read.PaymentMethodType, read.CardType, read.Invoice is not null, read.CreditNotes is not null));
    }

    /// <summary>
    /// An order with its invoice and notes stored reads them back as they were.
    /// </summary>
    [Fact]
    public void Deserialize_OrderWithItsInvoice_ReadsItBack()
    {
        // Arrange
        var order = OrdenDePrueba.Nueva();
        var invoice = Invoice(order.Id).Validate("SETP", 990000001, "cufe-1", new LocalDate(2026, 11, 10), Now, null, new FileAttachment(Guid.NewGuid(), "SETP990000001.pdf", "electronic-invoicing"));

        order.SetPaymentStatus(PaymentStatus.Succeeded, OrdenDePrueba.BuyerId);
        order.RequestElectronicDocuments(invoice, []);

        // Act
        var read = BsonSerializer.Deserialize<OrderAggregate>(order.ToBsonDocument());

        // Assert
        Assert.Equal((ElectronicDocumentState.Validated, "cufe-1", (LocalDate?)new LocalDate(2026, 11, 10), "SETP990000001.pdf"), (read.Invoice!.State, read.Invoice.Cufe, read.Invoice.IssueDate, read.Invoice.Pdf!.Name));
    }
}
