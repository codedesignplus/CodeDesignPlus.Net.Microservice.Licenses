using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Packages;

/// <summary>
/// Los paquetes en la suscripción de la copropiedad (pendings/739): cómo se combinan con la licencia, cuánto duran y qué
/// viaja a ms-tenants y a la factura.
/// </summary>
public class PackageSubscriptionTest
{
    private static Instant Now => SystemClock.Instance.GetCurrentInstant();

    /// <summary>
    /// Las reuniones se suman, los conectados toman el mayor y la grabación hace «o», cada uno con la regla que declara el
    /// catálogo; una clave que la licencia no trae la pone el paquete.
    /// </summary>
    [Fact]
    public void Combine_LicenseAndPackage_AppliesTheRuleOfEachAttribute()
    {
        // Arrange
        var license = new Dictionary<string, string> { ["assemblies_number_meetings"] = "2", ["assemblies_max_participants"] = "200", ["assemblies_enable_record"] = "false", ["ideal_for"] = "Conjuntos" };

        // Act
        var effective = AttributeCombiner.Combine(license, PackagePricing.Quote(PackageFixtures.Basic(), null).Attributes);

        // Assert
        Assert.Equal(("5", "200", "true", "true", "Conjuntos"), (effective["assemblies_number_meetings"], effective["assemblies_max_participants"], effective["assemblies_enable_record"], effective["assemblies_virtual_enable"], effective["ideal_for"]));
    }

    /// <summary>
    /// Con licencia anual el paquete comprado con ella vence con ella; con licencia mensual dura un año.
    /// </summary>
    [Fact]
    public void SetPaymentStatus_PackagesBoughtWithTheLicense_FollowTheLicenseTerm()
    {
        // Arrange
        var start = Now;

        // Act
        var annual = PackageFixtures.PaidPurchase(start, PackageFixtures.AnnualLicense(), PackageFixtures.Basic());
        var monthly = PackageFixtures.PaidPurchase(start, TestOrders.BuildLicense(), PackageFixtures.Basic());

        // Assert
        Assert.Equal(
            (annual.PeriodEnd, BillingPeriod.End(start, BillingType.Annually)),
            (annual.Packages![0].PeriodEnd, monthly.Packages![0].PeriodEnd));
    }

    /// <summary>
    /// La orden cobra la licencia y sus paquetes en un solo pago, y la factura los trae como líneas separadas.
    /// </summary>
    [Fact]
    public void ToBilling_APurchaseWithAPackage_ChargesBothAndBillsThemAsLines()
    {
        // Arrange
        var order = PackageFixtures.PaidPurchase(Now, TestOrders.BuildLicense(), PackageFixtures.Basic());

        // Act
        var billing = order.ToBilling(Now);

        // Assert
        Assert.Equal(
            (14_280_000L + 236_810_000L, 2, 236_810_000L),
            (order.AmountToCharge.Amount, billing.Lines.Count, billing.Lines.Single(x => x.PackageLineId is not null).Total.Amount));
    }

    /// <summary>
    /// El aprovisionamiento de la compra lleva los paquetes con su periodo para que ms-tenants los combine.
    /// </summary>
    [Fact]
    public void SetPaymentStatus_APurchaseWithAPackage_PublishesThePackagesForTheTenant()
    {
        // Arrange
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid(), TestOrders.BuildLicense(), CodeDesignPlus.Net.ValueObjects.User.Buyer.CreateMinimal(TestOrders.BuyerId, "Buyer", "+573001234567", "buyer@example.com"), TestOrders.BuildTenant(), TestOrders.BuyerId, [PackagePricing.Quote(PackageFixtures.Basic(), null).ToLine(Guid.NewGuid(), null)]);

        // Act
        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId);

        // Assert
        var @event = Assert.IsType<OrderPaidAndReadyForProvisioningDomainEvent>(order.GetAndClearEvents().Single());
        Assert.Equal(("Virtual Básico", "Sum"), (@event.Packages.Single().Name, @event.Packages.Single().Attributes.Single(x => x.Key == "assemblies_number_meetings").Combination));
    }

    /// <summary>
    /// Si la licencia vence y no se renueva, el paquete deja de contar aunque su periodo siga, y vuelve al pagarla
    /// (precisión del usuario, 2026-11-03).
    /// </summary>
    [Fact]
    public void EffectiveAttributes_LicenseNotPaid_SuspendsThePackages()
    {
        // Arrange
        var order = PackageFixtures.PaidPurchase(Now.Minus(Duration.FromDays(40)), TestOrders.BuildLicense(), PackageFixtures.Basic());
        var lines = PackageTerm.PaidLines([order]);

        // Act
        var lapsed = PackageTerm.EffectiveAttributes(order.License.Attributes, order.PeriodEnd!.Value, lines, Now);
        var paid = PackageTerm.EffectiveAttributes(order.License.Attributes, Now.Plus(Duration.FromDays(1)), lines, Now);

        // Assert
        Assert.Equal((false, "3"), (lapsed.ContainsKey("assemblies_number_meetings"), paid["assemblies_number_meetings"]));
    }

    /// <summary>
    /// Ampliar exige que ningún atributo baje y que alguno suba: reducir se hace al renovar.
    /// </summary>
    [Fact]
    public void QuoteAddOn_ReducingAPackage_IsRejected()
    {
        // Arrange
        var custom = PackageFixtures.Custom();
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid(), PackageFixtures.AnnualLicense(), CodeDesignPlus.Net.ValueObjects.User.Buyer.CreateMinimal(TestOrders.BuyerId, "Buyer", "+573001234567", "buyer@example.com"), TestOrders.BuildTenant(), TestOrders.BuyerId,
            [PackagePricing.Quote(custom, new Dictionary<string, string> { ["assemblies_number_meetings"] = "3" }).ToLine(Guid.NewGuid(), null)]);
        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, Now.Minus(Duration.FromDays(10)));

        var state = new TenantPackages(SubscriptionTimeline.From([order])!, PackageTerm.PaidLines([order]));

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => state.QuoteAddOn(custom, new Dictionary<string, string> { ["assemblies_number_meetings"] = "2" }, Money.Zero("COP"), Now));

        // Assert
        Assert.Equal("267", exception.Code);
    }

    /// <summary>
    /// Ampliar acredita lo no usado de la línea anterior con su IVA, y el paquete nuevo vence con la licencia anual.
    /// </summary>
    [Fact]
    public void QuoteAddOn_ExtendingAPackage_CreditsTheUnusedPartAndEndsWithTheLicense()
    {
        // Arrange
        var custom = PackageFixtures.Custom();
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid(), PackageFixtures.AnnualLicense(), CodeDesignPlus.Net.ValueObjects.User.Buyer.CreateMinimal(TestOrders.BuyerId, "Buyer", "+573001234567", "buyer@example.com"), TestOrders.BuildTenant(), TestOrders.BuyerId,
            [PackagePricing.Quote(custom, new Dictionary<string, string> { ["assemblies_number_meetings"] = "1" }).ToLine(Guid.NewGuid(), null)]);
        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, Now.Minus(Duration.FromDays(100)));

        var state = new TenantPackages(SubscriptionTimeline.From([order])!, PackageTerm.PaidLines([order]));

        // Act
        var quote = state.QuoteAddOn(custom, new Dictionary<string, string> { ["assemblies_number_meetings"] = "2" }, Money.Zero("COP"), Now);

        // Assert
        Assert.Equal(
            (order.Packages![0].LineId, true, order.PeriodEnd),
            (quote.ReplacesLineId, quote.Charge.ProrationCredit.Amount > 0 && quote.Charge.ProrationCredit.Amount < order.Packages[0].Total.Amount, (Instant?)quote.PeriodEnd));
    }

    /// <summary>
    /// Sin la licencia al día no se compra un paquete.
    /// </summary>
    [Fact]
    public void QuoteAddOn_LicenseNotPaid_IsRejected()
    {
        // Arrange
        var order = PackageFixtures.PaidPurchase(Now.Minus(Duration.FromDays(60)), TestOrders.BuildLicense());
        var state = new TenantPackages(SubscriptionTimeline.From([order])!, []);

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => state.QuoteAddOn(PackageFixtures.Basic(), null, Money.Zero("COP"), Now));

        // Assert
        Assert.Equal("269", exception.Code);
    }

    /// <summary>
    /// Con licencia mensual los paquetes no se renuevan con ella: pedirlo es un error, no se ignora.
    /// </summary>
    [Fact]
    public async Task RenewalQuotesAsync_PackagesWithAMonthlyLicense_AreRejected()
    {
        // Arrange
        var order = PackageFixtures.PaidPurchase(Now.Minus(Duration.FromDays(5)), TestOrders.BuildLicense(), PackageFixtures.Basic());
        var state = new TenantPackages(SubscriptionTimeline.From([order])!, PackageTerm.PaidLines([order]));

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => state.RenewalQuotesAsync(Mock.Of<IPackageRepository>(), [new PackageSelectionInput(PackageFixtures.BasicId, null)], Now, CancellationToken.None));

        // Assert
        Assert.Equal("272", exception.Code);
    }

    /// <summary>
    /// Con licencia anual, renovar sin decir nada renueva el paquete vigente con sus mismos valores y reemplaza su línea.
    /// </summary>
    [Fact]
    public async Task RenewalQuotesAsync_AnnualLicenseWithoutChoice_RenewsTheCurrentPackages()
    {
        // Arrange
        var basic = PackageFixtures.Basic();
        var order = PackageFixtures.PaidPurchase(Now.Minus(Duration.FromDays(5)), PackageFixtures.AnnualLicense(), basic);
        var state = new TenantPackages(SubscriptionTimeline.From([order])!, PackageTerm.PaidLines([order]));
        var packages = new Mock<IPackageRepository>();
        packages.Setup(x => x.FindAsync<PackageAggregate>(basic.Id, It.IsAny<CancellationToken>())).ReturnsAsync(basic);

        // Act
        var renewed = await state.RenewalQuotesAsync(packages.Object, null, Now, CancellationToken.None);

        // Assert
        Assert.Equal((basic.Id, (Guid?)order.Packages![0].LineId), (renewed.Single().Quote.Package.Id, renewed.Single().Replaces));
    }

    /// <summary>
    /// Una orden guardada antes de los paquetes se sigue leyendo (regla 31) y su total es el de la licencia.
    /// </summary>
    [Fact]
    public void Deserialize_AnOrderStoredBeforePackages_StillReads()
    {
        // Arrange
        CodeDesignPlus.Net.Mongo.Extensions.MongoSerializerRegistration.RegisterSerializers();

        var document = TestOrders.Paid().ToBsonDocument();
        document.Remove("Packages");

        // Act
        var stored = BsonSerializer.Deserialize<OrderAggregate>(document);

        // Assert
        Assert.Equal((null, 14_280_000L), (stored.Packages, stored.Total.Amount));
    }

    /// <summary>
    /// Un periodo acreditado guardado antes de los paquetes se sigue leyendo y es de la licencia (regla 31).
    /// </summary>
    [Fact]
    public void Deserialize_ACreditedPeriodStoredBeforePackages_IsTheLicense()
    {
        // Arrange
        CodeDesignPlus.Net.Mongo.Extensions.MongoSerializerRegistration.RegisterSerializers();

        var document = CreditedPeriod.Create(Guid.NewGuid(), Now, Now, 1, 30, Money.FromLong(100, "COP"), Money.FromLong(10, "COP"), Money.FromLong(2, "COP"), 1900).ToBsonDocument();
        document.Remove("LineId");

        // Act
        var stored = BsonSerializer.Deserialize<CreditedPeriod>(document);

        // Assert
        Assert.Null(stored.LineId);
    }
}
