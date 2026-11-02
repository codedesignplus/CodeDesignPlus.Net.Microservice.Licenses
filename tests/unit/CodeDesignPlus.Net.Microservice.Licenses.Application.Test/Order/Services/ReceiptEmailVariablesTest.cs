using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
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
