using System.Collections.Generic;
using System.Linq;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Packages;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;

/// <summary>
/// The events that follow a payment travel through the bus, which reads them with the serializer of the SDK
/// (<c>CodeDesignPlus.Net.Serializers.JsonSerializer</c>, Newtonsoft), and this same worker consumes them to invoice the
/// order (pendings/661). An event that does not read back goes to the dead letter after its retries, and the order is not
/// invoiced: on 2026-11-10 <c>LicenseTenant</c> carried the constructor attribute of System.Text.Json, which Newtonsoft
/// ignores.
/// </summary>
/// <remarks>
/// Each event comes from a real paid order, is written as the bus writes it, read back and written again: the two texts
/// must be the same, so nothing is lost on the way (a member with a private setter that Newtonsoft does not fill, for
/// example).
/// </remarks>
public class PaidEventsBusRoundTripTest
{
    private static T RoundTrip<T>(T @event, out string written, out string rewritten)
    {
        written = JsonSerializer.Serialize(@event);

        var read = JsonSerializer.Deserialize<T>(written);

        rewritten = JsonSerializer.Serialize(read);

        return read;
    }

    private static T PublishedBy<T>(OrderAggregate order) => order.GetAndClearEvents().OfType<T>().Single();

    [Fact]
    public void OrderPaidAndReadyForProvisioning_ThroughTheBus_ReadsBackWhole()
    {
        // Arrange
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid(), PlatformInvoiceTest.TaxedLicense(), Subscriptions.SubscriptionFixtures.Buyer(), PlatformInvoiceTest.TenantWithNit(), TestOrders.BuyerId);

        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, paymentMethodType: "CARD", cardType: "CREDIT");

        // Act
        RoundTrip(PublishedBy<OrderPaidAndReadyForProvisioningDomainEvent>(order), out var written, out var rewritten);

        // Assert
        Assert.Equal(written, rewritten);
    }

    [Fact]
    public void SubscriptionRenewed_ThroughTheBus_ReadsBackWhole()
    {
        // Arrange
        var order = Subscriptions.SubscriptionFixtures.AwaitingRenewal(700_000L);

        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, paymentMethodType: "PSE");

        // Act
        RoundTrip(PublishedBy<SubscriptionRenewedDomainEvent>(order), out var written, out var rewritten);

        // Assert
        Assert.Equal(written, rewritten);
    }

    [Fact]
    public void SubscriptionChanged_ThroughTheBus_ReadsBackWhole()
    {
        // Arrange
        var previous = PlatformInvoiceTest.PaidPurchase();
        var order = PlatformInvoiceTest.PaidChange(previous, 50_000L, publish: true);

        // Act
        var read = RoundTrip(PublishedBy<SubscriptionChangedDomainEvent>(order), out var written, out var rewritten);

        // Assert
        Assert.Equal((written, 2_204_000L), (rewritten, read.CreditedPeriods.Single().Tax!.Amount));
    }

    [Fact]
    public void SubscriptionPackagesPurchased_ThroughTheBus_ReadsBackWhole()
    {
        // Arrange
        var order = PackageFixtures.PackageOrder(BillingType.Monthly);

        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, paymentMethodType: "NEQUI");

        // Act
        RoundTrip(PublishedBy<SubscriptionPackagesPurchasedDomainEvent>(order), out var written, out var rewritten);

        // Assert
        Assert.Equal(written, rewritten);
    }
}
