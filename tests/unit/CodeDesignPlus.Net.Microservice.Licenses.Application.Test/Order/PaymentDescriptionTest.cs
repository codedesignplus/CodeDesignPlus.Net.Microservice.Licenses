using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Packages;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using Xunit;
using TenantSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.Tenant;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;

/// <summary>
/// The description of an order's payment in ms-payments, in Spanish with the order type and the property (pendings/674).
/// </summary>
/// <remarks>
/// The Super Admin reads it in Cobros de plataforma; it said «Payment for license Élite to tenant …».
/// </remarks>
public class PaymentDescriptionTest
{
    [Fact]
    public void For_Purchase_SaysTheLicenseAndTheProperty()
    {
        // Act
        var description = PaymentDescription.For(TestOrders.Build());

        // Assert
        Assert.Equal("Compra de la licencia Esencial para Conjunto Prueba", description);
    }

    [Fact]
    public void For_PurchaseWithAPackage_CountsThePackage()
    {
        // Arrange
        var order = PackageFixtures.PaidPurchase(SubscriptionFixtures.Now, PackageFixtures.AnnualLicense(), PackageFixtures.Basic());

        // Act
        var description = PaymentDescription.For(order);

        // Assert
        Assert.Equal("Compra de la licencia Estándar y 1 paquete para Conjunto Prueba", description);
    }

    [Fact]
    public void For_Renewal_SaysItIsARenewal()
    {
        // Arrange
        var order = SubscriptionFixtures.AwaitingRenewal();

        // Act
        var description = PaymentDescription.For(order);

        // Assert
        Assert.Equal($"Renovación de la licencia {order.License.Name} para Conjunto Prueba", description);
    }

    [Fact]
    public void For_Change_SaysTheLicenseItChangesTo()
    {
        // Act
        var description = PaymentDescription.For(ChangeValidationFixesTest.EliteChange());

        // Assert
        Assert.Equal("Cambio a la licencia Élite para Conjunto Prueba", description);
    }

    [Fact]
    public void For_Package_SaysThePackage()
    {
        // Act
        var description = PaymentDescription.For(PackageFixtures.PackageOrder(BillingType.Annually));

        // Assert
        Assert.Equal("Paquete Virtual Básico para Conjunto Prueba", description);
    }

    [Fact]
    public void For_LongestLicenseAndPropertyNames_FitsWhatMsPaymentsAccepts()
    {
        // Arrange: both names at their longest (124 characters each) go past the 255 that ms-payments accepts.
        var tenant = TestOrders.BuildTenant();
        var longName = TenantSnapshot.Create(tenant.Id, new string('A', 124), null, tenant.TypeDocument, tenant.NumberDocument, tenant.Phone, tenant.Email, tenant.Location);
        var order = Domain.OrderAggregate.Create(System.Guid.NewGuid(), System.Guid.NewGuid(), TestOrders.BuildLicense(new string('E', 124)), CodeDesignPlus.Net.ValueObjects.User.Buyer.CreateMinimal(TestOrders.BuyerId, "Buyer", "+573001234567", "buyer@example.com"), longName, TestOrders.BuyerId);

        // Act
        var description = PaymentDescription.For(order);

        // Assert
        Assert.Equal((PaymentDescription.MaxLength, true), (description.Length, description.EndsWith('…')));
    }
}
