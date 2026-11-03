using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order.Services;

/// <summary>
/// El correo «Suscripción renovada» (pendings/735, 738).
/// </summary>
public class RenewalEmailVariablesTest
{
    [Fact]
    public void Build_PaidRenewal_TotalIsWrittenWithTheSymbolAndTheColombianSeparators()
    {
        // Arrange
        var order = PaidRenewal();

        // Act
        var variables = RenewalEmailVariables.Build(order, TestOrders.Cop);

        // Assert
        Assert.Equal("$ 142.800,00", variables["total"]);
    }

    [Fact]
    public void Build_PaidRenewal_SaysThePaidPeriodInTheTenantCalendar()
    {
        // Arrange
        var order = PaidRenewal();

        // Act
        var variables = RenewalEmailVariables.Build(order, TestOrders.Cop);

        // Assert
        Assert.Equal("28 de nov de 2026 — 28 de dic de 2026", variables["period"]);
    }

    /// <summary>
    /// Una renovación pagada desde las 03:00 UTC del 29 de noviembre: aún el 28 en Bogotá.
    /// </summary>
    private static OrderAggregate PaidRenewal()
    {
        var order = SubscriptionFixtures.AwaitingRenewal();

        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, Instant.FromUtc(2026, 11, 29, 3, 0));

        return order;
    }
}
