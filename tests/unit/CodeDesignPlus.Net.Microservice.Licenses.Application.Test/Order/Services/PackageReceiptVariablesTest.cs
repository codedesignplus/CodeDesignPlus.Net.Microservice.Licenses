using System.Collections.Generic;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Packages;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using LicenseSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.License;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order.Services;

/// <summary>
/// The receipt of a package purchase shows the package with its base, its tax, its total and its term, not the modules of
/// the license (pendings/739).
/// </summary>
public class PackageReceiptVariablesTest
{
    private static readonly Instant PaidAt = Instant.FromUtc(2026, 11, 3, 15, 0);

    [Fact]
    public void ReceiptTemplateOf_PackageOrder_IsThePackageReceipt()
    {
        // Arrange
        var order = PaidPackageOrder(BillingType.Monthly);

        // Act
        var template = ReceiptEmailVariables.ReceiptTemplateOf(order);

        // Assert
        Assert.Equal("PackageReceipt", template);
    }

    [Fact]
    public void Build_PackageOrder_BreaksThePackageIntoBaseTaxAndTotal()
    {
        // Arrange
        var order = PaidPackageOrder(BillingType.Monthly);

        // Act
        var variables = ReceiptEmailVariables.Build(order, TestOrders.Cop);

        // Assert
        Assert.Equal(("$ 1.990.000,00", "IVA (19 %)", "$ 378.100,00", "$ 2.368.100,00"), (variables["package_subtotal"], variables["package_tax_label"], variables["package_tax"], variables["package_total"]));
    }

    [Fact]
    public void Build_PackageOrderWithAMonthlyLicense_LastsTwelveMonths()
    {
        // Arrange
        var order = PaidPackageOrder(BillingType.Monthly);

        // Act
        var variables = ReceiptEmailVariables.Build(order, TestOrders.Cop);

        // Assert
        Assert.Equal(("3 de nov de 2026 — 3 de nov de 2027", true), (variables["package_period"], variables["package_term"].StartsWith("Dura doce meses desde la compra", StringComparison.Ordinal)));
    }

    [Fact]
    public void Build_PackageOrderWithAnAnnualLicense_EndsWithTheLicensePeriod()
    {
        // Arrange
        var order = PaidPackageOrder(BillingType.Annually, licensePeriodEnd: Instant.FromUtc(2027, 3, 1, 5, 0));

        // Act
        var variables = ReceiptEmailVariables.Build(order, TestOrders.Cop);

        // Assert
        Assert.Equal(("3 de nov de 2026 — 1 de mar de 2027", true), (variables["package_period"], variables["package_term"].StartsWith("Vence con el periodo de tu licencia anual", StringComparison.Ordinal)));
    }

    [Fact]
    public void Build_LicensePurchase_CarriesNoPackageVariable()
    {
        // Arrange
        var order = PackageFixtures.PaidPurchase(PaidAt, PackageFixtures.AnnualLicense(), PackageFixtures.Basic());

        // Act
        var variables = ReceiptEmailVariables.Build(order, TestOrders.Cop);

        // Assert
        Assert.DoesNotContain(variables.Keys, x => x.StartsWith("package_", StringComparison.Ordinal));
    }

    /// <summary>
    /// A package order does not charge the license: the receipt carries exactly the variables of the PackageReceipt
    /// template, never the license ones with a $ 0 subtotal, «IVA (0 %)» or its modules.
    /// </summary>
    [Fact]
    public void Build_PackageOrder_CarriesOnlyTheVariablesOfThePackageReceipt()
    {
        // Arrange
        var order = PaidPackageOrder(BillingType.Monthly);
        string[] expected =
        [
            "organization_name", "organization_email", "organization_phone", "organization_document", "buyer_name", "buyer_email", "order_id",
            "purchase_date", "current_year", "package_name", "package_concept", "package_subtotal", "package_tax_label", "package_tax",
            "package_tax_row_display", "package_total", "credit_applied", "credit_row_display", "amount_paid", "package_period", "package_term"
        ];

        // Act
        var variables = ReceiptEmailVariables.Build(order, TestOrders.Cop);

        // Assert
        Assert.Equal(expected.Order(StringComparer.Ordinal), variables.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void Build_TaxedPackage_ShowsTheTaxRow()
    {
        // Arrange
        var order = PaidPackageOrder(BillingType.Monthly);

        // Act
        var variables = ReceiptEmailVariables.Build(order, TestOrders.Cop);

        // Assert
        Assert.Equal("table-row", variables["package_tax_row_display"]);
    }

    /// <summary>
    /// A package without IVA hides the tax row: no row of the receipt shows $ 0.
    /// </summary>
    [Fact]
    public void Build_PackageWithoutTax_HidesTheTaxRow()
    {
        // Arrange
        var order = PaidPackageOrder(BillingType.Monthly, untaxed: true);

        // Act
        var variables = ReceiptEmailVariables.Build(order, TestOrders.Cop);

        // Assert
        Assert.Equal(("none", "none"), (variables["package_tax_row_display"], variables["credit_row_display"]));
    }

    /// <summary>
    /// The email of a package purchase carries exactly the variables of the SubscriptionPackagePurchased template: the
    /// package, its term, its price, the credit and what was paid.
    /// </summary>
    [Fact]
    public void BuildPackage_PackageOrder_CarriesOnlyTheVariablesOfThePackageEmail()
    {
        // Arrange
        var order = PaidPackageOrder(BillingType.Monthly);
        string[] expected = ["organization_name", "license_name", "period", "price", "credit_applied", "credit_row_display", "amount_paid", "current_year"];

        // Act
        var variables = ChangeEmailVariables.BuildPackage(order, TestOrders.Cop);

        // Assert
        Assert.Equal(expected.Order(StringComparer.Ordinal), variables.Keys.Order(StringComparer.Ordinal));
    }

    [Fact]
    public void BuildPackage_PackageOrder_NamesThePackageWithItsTermAndPrice()
    {
        // Arrange
        var order = PaidPackageOrder(BillingType.Monthly);

        // Act
        var variables = ChangeEmailVariables.BuildPackage(order, TestOrders.Cop);

        // Assert
        Assert.Equal(("Virtual Básico", "3 de nov de 2026 — 3 de nov de 2027", "$ 2.368.100,00", "$ 2.368.100,00"), (variables["license_name"], variables["period"], variables["price"], variables["amount_paid"]));
    }

    /// <summary>
    /// Virtual Básico bought for an existing tenant, paid at <see cref="PaidAt"/>.
    /// </summary>
    private static OrderAggregate PaidPackageOrder(BillingType billingType, Instant? licensePeriodEnd = null, bool untaxed = false)
    {
        var order = PackageFixtures.PackageOrder(billingType, untaxed);

        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, PaidAt, licensePeriodEnd: licensePeriodEnd);
        order.GetAndClearEvents();

        return order;
    }
}
