using System.Collections.Generic;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using LicenseSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.License;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order.Services;

/// <summary>
/// Lo que lee el comprador en el correo y el recibo de compra (pendings/264).
/// </summary>
public class ReceiptEmailVariablesTest
{
    [Fact]
    public void Build_TenantDocument_IsTheCodeOfTheTypeAndTheNumber()
    {
        // Arrange
        var order = PaidAt(Instant.FromUtc(2026, 11, 2, 15, 0));

        // Act
        var variables = ReceiptEmailVariables.Build(order, 2);

        // Assert
        Assert.Equal("NIT 900123456", variables["organization_document"]);
    }

    [Fact]
    public void Build_AnyValue_NeverCarriesTheNameOfADotNetType()
    {
        // Arrange
        var order = PaidAt(Instant.FromUtc(2026, 11, 2, 15, 0));

        // Act
        var variables = ReceiptEmailVariables.Build(order, 2);

        // Assert
        Assert.DoesNotContain(variables, x => x.Value.Contains("CodeDesignPlus.", StringComparison.Ordinal) || x.Value.Contains("NodaTime", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(BillingType.Monthly, "Mensual")]
    [InlineData(BillingType.Annually, "Anual")]
    public void Build_BillingType_IsInTheLanguageOfTheTemplate(BillingType billingType, string expected)
    {
        // Arrange
        var order = PaidAt(Instant.FromUtc(2026, 11, 2, 15, 0), billingType);

        // Act
        var variables = ReceiptEmailVariables.Build(order, 2);

        // Assert
        Assert.Equal(expected, variables["billing_type"]);
    }

    [Fact]
    public void Build_PaidAtNightInBogota_PurchaseDateIsTheTenantCalendarDay()
    {
        // Arrange: 03:00 UTC del 2 de noviembre son las 22:00 del 1 de noviembre en Bogotá.
        var order = PaidAt(Instant.FromUtc(2026, 11, 2, 3, 0));

        // Act
        var variables = ReceiptEmailVariables.Build(order, 2);

        // Assert
        Assert.Equal("01/11/2026", variables["purchase_date"]);
    }

    [Fact]
    public void Build_PaidOnNewYearsEveInBogota_CurrentYearIsTheTenantYearOfThePurchase()
    {
        // Arrange: 03:00 UTC del 1 de enero de 2031 aún es 31 de diciembre de 2030 en Bogotá.
        var order = PaidAt(Instant.FromUtc(2031, 1, 1, 3, 0));

        // Act
        var variables = ReceiptEmailVariables.Build(order, 2);

        // Assert
        Assert.Equal("2030", variables["current_year"]);
    }

    /// <summary>
    /// Un cambio de licencia que paga una parte con el crédito por lo no usado: el recibo muestra el precio, el crédito
    /// aplicado y lo que de verdad se pagó, no el precio completo como si se hubiera cobrado (pendings/738).
    /// </summary>
    [Fact]
    public void Build_ChangePaidPartlyWithCredit_ShowsPriceCreditAppliedAndAmountPaid()
    {
        // Arrange
        var order = ChangeWithCredit(4_280_000);

        // Act
        var variables = ReceiptEmailVariables.Build(order, 2);

        // Assert
        Assert.Equal((142_800m.ToString("N2"), 42_800m.ToString("N2"), 100_000m.ToString("N2")), (variables["price"], variables["credit_applied"], variables["amount_paid"]));
    }

    /// <summary>
    /// Con un crédito mayor que el precio no se paga nada: el crédito aplicado es el precio y el sobrante queda como saldo a
    /// favor, que no es de este recibo.
    /// </summary>
    [Fact]
    public void Build_ChangeCoveredByTheCredit_AppliesThePriceAndPaysNothing()
    {
        // Arrange
        var order = ChangeWithCredit(16_000_000);

        // Act
        var variables = ReceiptEmailVariables.Build(order, 2);

        // Assert
        Assert.Equal((142_800m.ToString("N2"), 0m.ToString("N2")), (variables["credit_applied"], variables["amount_paid"]));
    }

    [Fact]
    public void Build_RenewalWithCreditBalance_AmountPaidDiscountsTheBalance()
    {
        // Arrange
        var order = SubscriptionFixtures.AwaitingRenewal(creditBalanceApplied: 2_280_000);

        // Act
        var variables = ReceiptEmailVariables.Build(order, 2);

        // Assert
        Assert.Equal((22_800m.ToString("N2"), 120_000m.ToString("N2")), (variables["credit_applied"], variables["amount_paid"]));
    }

    [Fact]
    public void ReceiptTemplateOf_ChangeOrRenewal_IsTheSubscriptionReceipt()
    {
        // Arrange
        var orders = new[] { ChangeWithCredit(4_280_000), SubscriptionFixtures.AwaitingRenewal() };

        // Act
        var templates = orders.Select(ReceiptEmailVariables.ReceiptTemplateOf).Distinct().ToList();

        // Assert
        Assert.Equal(["SubscriptionReceipt"], templates);
    }

    [Fact]
    public void ReceiptTemplateOf_Purchase_IsThePurchaseReceipt()
    {
        // Arrange
        var order = PaidAt(Instant.FromUtc(2026, 11, 2, 15, 0));

        // Act
        var template = ReceiptEmailVariables.ReceiptTemplateOf(order);

        // Assert
        Assert.Equal("PurchaseReceipt", template);
    }

    /// <summary>
    /// Un cambio a la licencia de prueba (142.800,00 COP con IVA) con <paramref name="credit"/> de crédito por lo no usado.
    /// </summary>
    private static OrderAggregate ChangeWithCredit(long credit)
    {
        var license = TestOrders.BuildLicense();
        var now = Instant.FromUtc(2026, 11, 2, 15, 0);
        var credited = new List<CreditedPeriod> { new(Guid.NewGuid(), now, now.Plus(Duration.FromDays(20)), 20, 30, Money.FromLong(20_000_000, "COP"), Money.FromLong(credit, "COP")) };
        var charge = SubscriptionCharge.Compute(license.Total, Money.FromLong(credit, "COP"), Money.Zero("COP"));

        return OrderAggregate.CreateChange(Guid.NewGuid(), Guid.NewGuid(), license, Buyer(), TestOrders.BuildTenant(), Guid.NewGuid(), credited, charge, TestOrders.BuyerId);
    }

    /// <summary>
    /// Una compra cuyo pago se aprobó en <paramref name="paidAt"/>.
    /// </summary>
    private static OrderAggregate PaidAt(Instant paidAt, BillingType billingType = BillingType.Monthly)
    {
        var order = OrderAggregate.Create(TestOrders.OrderId, TestOrders.PaymentId, License(billingType), Buyer(), TestOrders.BuildTenant(), TestOrders.BuyerId);

        order.ProvisioningHistory.Add(new ProvisioningStep(ProvisioningStep.Payment, ProvisioningStepStatus.Completed, paidAt));

        return order;
    }

    private static LicenseSnapshot License(BillingType billingType)
    {
        var license = TestOrders.BuildLicense();

        return LicenseSnapshot.Create(
            license.Id, license.Name, license.Total, license.Tax, license.SubTotal, billingType, license.BillingModel,
            license.Description, license.ShortDescription, license.Icon, license.TermsOfService, license.IsPopular,
            license.ShowInLandingPage, license.Attributes, license.Modules);
    }

    private static CodeDesignPlus.Net.ValueObjects.User.Buyer Buyer() => CodeDesignPlus.Net.ValueObjects.User.Buyer.CreateMinimal(TestOrders.BuyerId, "Buyer", "+573001234567", "buyer@example.com");
}
