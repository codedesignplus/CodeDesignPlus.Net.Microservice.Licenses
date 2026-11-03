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
    public void Build_LicensePurchase_LeavesThePackageVariablesEmpty()
    {
        // Arrange
        var order = PackageFixtures.PaidPurchase(PaidAt, PackageFixtures.AnnualLicense(), PackageFixtures.Basic());

        // Act
        var variables = ReceiptEmailVariables.Build(order, TestOrders.Cop);

        // Assert
        Assert.Equal((string.Empty, string.Empty), (variables["package_total"], variables["package_period"]));
    }

    /// <summary>
    /// Virtual Básico ($ 1.990.000 + IVA del 19 %) bought for an existing tenant, paid at <see cref="PaidAt"/>.
    /// </summary>
    private static OrderAggregate PaidPackageOrder(BillingType billingType, Instant? licensePeriodEnd = null)
    {
        var line = PackagePricing.Quote(PackageFixtures.Basic(), null).ToLine(Guid.NewGuid(), null);
        var reference = PackageFixtures.AnnualLicense();
        var zero = PackageFixtures.Cop(0);
        var license = LicenseSnapshot.Create(
            reference.Id, reference.Name, zero, zero, zero, billingType, reference.BillingModel, reference.Description, reference.ShortDescription,
            reference.Icon, reference.TermsOfService, reference.IsPopular, reference.ShowInLandingPage, reference.Attributes, reference.Modules);
        var charge = SubscriptionCharge.Compute(line.Total, zero, zero);
        var order = OrderAggregate.CreatePackagePurchase(Guid.NewGuid(), Guid.NewGuid(), license, Buyer(), TestOrders.BuildTenant(), line, new List<CreditedPeriod>(), charge, TestOrders.BuyerId);

        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, PaidAt, licensePeriodEnd: licensePeriodEnd);
        order.GetAndClearEvents();

        return order;
    }

    private static CodeDesignPlus.Net.ValueObjects.User.Buyer Buyer() => CodeDesignPlus.Net.ValueObjects.User.Buyer.CreateMinimal(TestOrders.BuyerId, "Buyer", "+573001234567", "buyer@example.com");
}
