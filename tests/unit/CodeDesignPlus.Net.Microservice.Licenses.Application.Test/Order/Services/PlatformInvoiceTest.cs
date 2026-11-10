using System.Collections.Generic;
using System.Linq;
using CodeDesignPlus.Net.gRpc.Clients.Services.ElectronicInvoicing;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.User;
using Xunit;
using TenantSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.Tenant;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order.Services;

/// <summary>
/// The electronic documents of a paid order as ms-electronicinvoicing receives them (pendings/661).
/// </summary>
public class PlatformInvoiceTest
{
    private static readonly Instant PaidAt = Instant.FromUtc(2026, 11, 10, 15, 0);

    /// <summary>
    /// The options as the worker's appsettings sets them for Colombia.
    /// </summary>
    internal static PlatformInvoicingOptions Options() => new()
    {
        Enable = true,
        ReferencePrefix = "license-order",
        BuyerPersonTypeCode = "1",
        BuyerTaxLiabilityCode = "ZZ",
        BuyerTaxResponsibilities = ["R-99-PN"],
        IdentificationTypeCodes = new(StringComparer.OrdinalIgnoreCase) { ["NIT"] = "31", ["CC"] = "13" },
        PaymentFormCode = "1",
        PaymentMeansCodes = new(StringComparer.OrdinalIgnoreCase) { ["PSE"] = "47", ["NEQUI"] = "47", ["BANCOLOMBIA_TRANSFER"] = "47", ["DAVIPLATA"] = "47" },
        CardPaymentMethodType = "CARD",
        CardPaymentMeansCodes = new(StringComparer.OrdinalIgnoreCase) { ["CREDIT"] = "48", ["DEBIT"] = "49" },
        UnknownPaymentMeansCode = "1",
        FullyPrepaidPaymentMeansCode = "ZZZ",
        UnitCode = "94",
        VatTaxCode = "01",
        VatTaxName = "IVA",
        CreditNoteCorrectionConceptCode = "1"
    };

    /// <summary>
    /// The license of the tests with VAT at 19 % and its concept, as the checkout copies it (pendings/662).
    /// </summary>
    internal static Domain.ValueObjects.License TaxedLicense(string name = "Esencial") => TestOrders.BuildLicense(name)
        .WithBillingTerms(Price.Create(BillingType.Monthly, Money.FromLong(12_000_000L, "COP"), BillingModel.FlatRate, 0, TaxTreatment.Taxed, 1900, null), $"Suscripción a la plataforma Kappali (software como servicio), plan {name}");

    internal static TenantSnapshot TenantWithNit(string number = "901888777-3", string typeCode = "NIT") => TenantSnapshot.Create(
        TestOrders.TenantId, "Conjunto Las Acacias", null, TypeDocument.Create(typeCode, "Documento"), number, "+573001234567", "admin@acacias.com", TestOrders.BuildTenant().Location);

    /// <summary>
    /// A purchase paid with the instrument given.
    /// </summary>
    internal static OrderAggregate PaidPurchase(string? method = "CARD", string? cardType = "CREDIT", Guid? id = null)
    {
        var order = OrderAggregate.Create(id ?? Guid.NewGuid(), Guid.NewGuid(), TaxedLicense(), SubscriptionFixtures.Buyer(), TenantWithNit(), TestOrders.BuyerId);

        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, PaidAt, paymentMethodType: method, cardType: cardType);
        order.GetAndClearEvents();

        return order;
    }

    /// <summary>
    /// A paid change to «Élite» that credits 29 of the 30 days of <paramref name="corrected"/>, with its VAT, plus some
    /// balance in favour.
    /// </summary>
    internal static OrderAggregate PaidChange(OrderAggregate corrected, long balance = 0)
    {
        var license = TaxedLicense("Élite");
        var credited = new List<CreditedPeriod> { CreditedPeriod.Create(corrected.Id, PaidAt, PaidAt.Plus(Duration.FromDays(29)), 29, 30, Money.FromLong(12_000_000L, "COP"), Money.FromLong(11_600_000L, "COP"), Money.FromLong(2_204_000L, "COP"), 1900) };
        var charge = SubscriptionCharge.Compute(license.Total, Money.FromLong(13_804_000L, "COP"), Money.FromLong(balance, "COP"));
        var order = OrderAggregate.CreateChange(Guid.NewGuid(), Guid.NewGuid(), license, SubscriptionFixtures.Buyer(), TenantWithNit(), corrected.License.Id, credited, charge, TestOrders.BuyerId);

        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, paymentMethodType: "PSE");
        order.GetAndClearEvents();

        return order;
    }

    internal static OrderElectronicDocument ValidatedInvoiceOf(OrderAggregate order) => OrderElectronicDocument
        .Requested(PlatformInvoice.InvoiceIdOf(order.Id), PlatformInvoice.InvoiceReferenceOf(Options(), order.Id), null, PaidAt)
        .Validate("SETP", 990000001, "cufe-1", new LocalDate(2026, 11, 10), PaidAt, null, null);

    [Fact]
    public void Invoice_PaidPurchase_IsIssuedByThePlatformWithoutTenant()
    {
        // Act
        var request = PlatformInvoice.Invoice(PaidPurchase(), new Dictionary<Guid, OrderAggregate>(), Options(), null);

        // Assert
        Assert.Equal((true, false, DocumentType.Invoice), (request.PlatformIssuer, request.HasIssuerTenant, request.Type));
    }

    /// <summary>
    /// The id and the reference come from the order: a retry asks for the same document, and the events find the order.
    /// </summary>
    [Fact]
    public void Invoice_SameOrderTwice_HasTheSameIdAndReference()
    {
        // Arrange
        var order = PaidPurchase();

        // Act
        var first = PlatformInvoice.Invoice(order, new Dictionary<Guid, OrderAggregate>(), Options(), null);
        var second = PlatformInvoice.Invoice(order, new Dictionary<Guid, OrderAggregate>(), Options(), null);

        // Assert
        Assert.Equal((first.Id, $"license-order:{order.Id}", first.Content), (second.Id, second.Reference, second.Content));
    }

    [Fact]
    public void InvoiceIdOf_TwoOrders_AreDifferent()
    {
        // Act
        var first = PlatformInvoice.InvoiceIdOf(Guid.NewGuid());
        var second = PlatformInvoice.InvoiceIdOf(Guid.NewGuid());

        // Assert
        Assert.NotEqual(first, second);
    }

    /// <summary>
    /// The line says the concept of the license and its VAT at 19 % on the base, in minor units.
    /// </summary>
    [Fact]
    public void Invoice_TaxedLicense_CarriesItsConceptAndItsVat()
    {
        // Act
        var line = PlatformInvoice.Invoice(PaidPurchase(), new Dictionary<Guid, OrderAggregate>(), Options(), null).Content.Lines.Single();
        var tax = line.Taxes.Single();

        // Assert
        Assert.Equal(
            ("Suscripción a la plataforma Kappali (software como servicio), plan Esencial", "1", "94", 12_000_000L, "01", "IVA", 1900, 12_000_000L, 2_280_000L),
            (line.Description, line.Quantity, line.UnitCode, line.Amount, tax.Code, tax.Name, tax.RateBasisPoints, tax.BaseAmount, tax.Amount));
    }

    /// <summary>
    /// A purchase paid in full: what is payable is the price with VAT and nothing was paid in advance.
    /// </summary>
    [Fact]
    public void Invoice_PaidPurchase_IsPayableInFull()
    {
        // Act
        var totals = PlatformInvoice.Invoice(PaidPurchase(), new Dictionary<Guid, OrderAggregate>(), Options(), null).Content.Totals;

        // Assert
        Assert.Equal(("COP", 12_000_000L, 2_280_000L, 0L, 0L, 14_280_000L), (totals.Currency, totals.GrossAmount, totals.TaxAmount, totals.WithholdingAmount, totals.PrepaymentAmount, totals.PayableAmount));
    }

    /// <summary>
    /// The buyer has no VAT responsibility, so nothing is withheld, and the formula of ms-electronicinvoicing holds:
    /// payable = gross - discount + tax - prepayments.
    /// </summary>
    [Fact]
    public void Invoice_Totals_FollowTheFormulaOfElectronicInvoicing()
    {
        // Arrange
        var corrected = PaidPurchase();

        // Act
        var totals = PlatformInvoice.Invoice(PaidChange(corrected, 50_000L), new Dictionary<Guid, OrderAggregate> { [corrected.Id] = corrected }, Options(), null).Content.Totals;

        // Assert
        Assert.Equal(totals.GrossAmount - totals.DiscountAmount + totals.TaxAmount - totals.PrepaymentAmount, totals.PayableAmount);
    }

    /// <summary>
    /// A change says the full price of the new license; the credit of the previous order and the balance in favour are
    /// prepayments, the credit dated on the payment of the previous order, and what is payable is what the gateway charged.
    /// </summary>
    [Fact]
    public void Invoice_PaidChange_CarriesTheCreditAndTheBalanceAsPrepayments()
    {
        // Arrange
        var corrected = PaidPurchase();
        var order = PaidChange(corrected, 50_000L);

        // Act
        var content = PlatformInvoice.Invoice(order, new Dictionary<Guid, OrderAggregate> { [corrected.Id] = corrected }, Options(), null).Content;

        // Assert
        var paidOn = NodaTime.Text.LocalDatePattern.Iso.Format(corrected.PaidAt!.Value.InZone(DateTimeZoneProviders.Tzdb["America/Bogota"]).Date);

        Assert.Equal(
            (13_804_000L, paidOn, 50_000L, order.AmountToCharge.Amount),
            (content.Prepayments[0].Amount, content.Prepayments[0].ReceivedOn, content.Prepayments[1].Amount, content.Totals.PayableAmount));
    }

    /// <summary>
    /// A renewal paid in part with the balance in favour: the balance is a prepayment.
    /// </summary>
    [Fact]
    public void Invoice_RenewalWithBalance_CarriesTheBalanceAsPrepayment()
    {
        // Arrange
        var order = SubscriptionFixtures.AwaitingRenewal(700_000L);

        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, PaidAt, paymentMethodType: "NEQUI");

        // Act
        var content = PlatformInvoice.Invoice(order, new Dictionary<Guid, OrderAggregate>(), Options(), null).Content;

        // Assert
        Assert.Equal((700_000L, order.AmountToCharge.Amount), (content.Prepayments.Single().Amount, content.Totals.PayableAmount));
    }

    /// <summary>
    /// Cash form always, and the payment means of the instrument the payer really used, as the accountant decided.
    /// </summary>
    [Theory]
    [InlineData("CARD", "CREDIT", "48")]
    [InlineData("CARD", "DEBIT", "49")]
    [InlineData("CARD", null, "1")]
    [InlineData("PSE", null, "47")]
    [InlineData("BANCOLOMBIA_TRANSFER", null, "47")]
    [InlineData("NEQUI", null, "47")]
    [InlineData("DAVIPLATA", null, "47")]
    [InlineData("SOMETHING_NEW", null, "1")]
    [InlineData(null, null, "1")]
    public void Invoice_Instrument_IsTranslatedToThePaymentMeans(string? method, string? cardType, string expected)
    {
        // Act
        var means = PlatformInvoice.Invoice(PaidPurchase(method, cardType), new Dictionary<Guid, OrderAggregate>(), Options(), null).Content.PaymentMeans;

        // Assert
        Assert.Equal(("1", expected), (means.FormCode, means.MethodCode));
    }

    /// <summary>
    /// An order the gateway charged nothing for, because the credit and the balance paid it all, is a mutual agreement.
    /// </summary>
    [Fact]
    public void PaymentMeansCodeOf_NothingCharged_IsMutualAgreement()
    {
        // Arrange
        var order = SubscriptionFixtures.AwaitingRenewal(TestOrders.BuildLicense().Total.Amount);

        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, PaidAt);

        // Act
        var code = PlatformInvoice.PaymentMeansCodeOf(order, Options());

        // Assert
        Assert.Equal("ZZZ", code);
    }

    /// <summary>
    /// The copropiedad is the buyer: a legal person, its NIT split from the check digit after the dash, the responsibility
    /// «not applicable» and no address until the checkout asks for the municipality.
    /// </summary>
    [Fact]
    public void BuyerOf_TenantWithNit_SplitsTheCheckDigitAndHasNoAddress()
    {
        // Act
        var buyer = PlatformInvoice.BuyerOf(TenantWithNit("901.888.777-3"), Options());

        // Assert
        Assert.Equal(
            ("1", "31", "901888777", "3", "Conjunto Las Acacias", "admin@acacias.com", "+573001234567", "ZZ", "R-99-PN", false),
            (buyer.PersonTypeCode, buyer.IdentificationTypeCode, buyer.IdentificationNumber, buyer.CheckDigit, buyer.Name, buyer.Email, buyer.Phone, buyer.TaxLiabilityCode, buyer.TaxResponsibilities.Single(), buyer.Address is not null));
    }

    [Fact]
    public void BuyerOf_NumberWithoutCheckDigit_SendsNone()
    {
        // Act
        var buyer = PlatformInvoice.BuyerOf(TenantWithNit("1020304050", "CC"), Options());

        // Assert
        Assert.Equal(("13", "1020304050", false), (buyer.IdentificationTypeCode, buyer.IdentificationNumber, buyer.HasCheckDigit));
    }

    /// <summary>
    /// A document type without its code in the configuration is not guessed: it fails so the message is retried and the
    /// log says what is missing.
    /// </summary>
    [Fact]
    public void BuyerOf_DocumentTypeWithoutCode_Fails()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => PlatformInvoice.BuyerOf(TenantWithNit("A123", "XX"), Options()));
    }

    [Theory]
    [InlineData("license-order:aa000000-0000-4000-8000-000000000101", true)]
    [InlineData("license-order:aa000000-0000-4000-8000-000000000101:credit-note:bb000000-0000-4000-8000-000000000102", true)]
    [InlineData("invoicing:aa000000-0000-4000-8000-000000000101", false)]
    [InlineData("receipt-order:aa000000-0000-4000-8000-000000000101", false)]
    [InlineData("license-order:not-a-guid", false)]
    [InlineData("", false)]
    public void OrderIdOf_Reference_IsAnOrderOnlyWhenItIsOurs(string reference, bool isOrder)
    {
        // Act
        var orderId = PlatformInvoice.OrderIdOf(Options(), reference);

        // Assert
        Assert.Equal(isOrder, orderId == Guid.Parse("aa000000-0000-4000-8000-000000000101"));
    }

    /// <summary>
    /// The credit note corrects the validated invoice of the previous order by its id, with the concept the accountant
    /// chose, and returns the base and the VAT of the days not served.
    /// </summary>
    [Fact]
    public void CreditNote_PaidChange_ReturnsTheUnusedDaysOnTheInvoiceOfThePreviousOrder()
    {
        // Arrange
        var corrected = PaidPurchase();
        var invoice = ValidatedInvoiceOf(corrected);
        var order = PaidChange(corrected);

        // Act
        var request = PlatformInvoice.CreditNote(order, corrected, invoice, Options());
        var line = request.Content.Lines.Single();

        // Assert
        Assert.Equal(
            (DocumentType.CreditNote, true, invoice.DocumentId.ToString(), "1", 11_600_000L, 2_204_000L, 13_804_000L, PlatformInvoice.CreditNoteIdOf(order.Id, corrected.Id).ToString()),
            (request.Type, request.PlatformIssuer, request.Content.BillingReference.DocumentId, request.Content.CorrectionConceptCode, line.Amount, line.Taxes.Single().Amount, request.Content.Totals.PayableAmount, request.Id));
    }

    [Fact]
    public void CreditNote_PaidChange_SaysThePeriodAndTheNewPlan()
    {
        // Arrange
        var corrected = PaidPurchase();
        var order = PaidChange(corrected);

        // Act
        var description = PlatformInvoice.CreditNote(order, corrected, ValidatedInvoiceOf(corrected), Options()).Content.Lines.Single().Description;

        // Assert
        Assert.Equal("Devolución parcial: 29 de 30 días no prestados del periodo del 10/11/2026 al 09/12/2026, por cambio al plan Élite", description);
    }
}
