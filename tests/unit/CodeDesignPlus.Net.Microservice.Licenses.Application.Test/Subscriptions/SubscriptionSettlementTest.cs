using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;

/// <summary>
/// Lo que sigue al pago de un cambio o una renovación (pendings/738): la licencia anterior se inactiva y el saldo a favor
/// recibe lo que sobra.
/// </summary>
public class SubscriptionSettlementTest
{
    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<ICreditBalanceRepository> creditBalances = new();
    private readonly CreditBalanceAggregate balance = CreditBalanceAggregate.Create(TestOrders.TenantId, "COP");
    private readonly SubscriptionSettlement settlement;

    public SubscriptionSettlementTest()
    {
        creditBalances.AppliesTo(balance);
        settlement = new SubscriptionSettlement(orders.Object, creditBalances.Object, Mock.Of<ILogger<SubscriptionSettlement>>());
    }

    /// <summary>
    /// Una licencia a medida más barata que lo no prestado (pendings/464): la orden anterior se inactiva y el sobrante del
    /// crédito queda como saldo a favor para las renovaciones siguientes.
    /// </summary>
    [Fact]
    public async Task SettleAsync_AChangeCheaperThanTheCredit_DeactivatesThePreviousOrderAndKeepsTheRestAsCreditBalance()
    {
        // Arrange
        var previous = Guid.NewGuid();
        var license = TestOrders.BuildLicense();
        var credited = new List<CreditedPeriod> { new(previous, SubscriptionFixtures.Now, SubscriptionFixtures.Now.Plus(Duration.FromDays(20)), 20, 30, Money.FromLong(20_000_000, "COP"), Money.FromLong(16_000_000, "COP")) };
        var charge = SubscriptionCharge.Compute(license.Total, Money.FromLong(16_000_000, "COP"), Money.Zero("COP"));
        var order = OrderAggregate.CreateChange(Guid.NewGuid(), Guid.NewGuid(), license, SubscriptionFixtures.Buyer(), TestOrders.BuildTenant(), Guid.NewGuid(), credited, charge, TestOrders.BuyerId);
        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId);

        // Act
        await settlement.SettleAsync(order, CancellationToken.None);

        // Assert
        Assert.Equal(16_000_000L - 14_280_000L, balance.Balance.Amount);
        orders.Verify(x => x.DeactivateAsync(It.Is<IReadOnlyCollection<Guid>>(ids => ids.Count == 1 && ids.Contains(previous)), TestOrders.BuyerId, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Una orden reemplazada que alguien pagó igual no cambia la licencia: lo pagado queda como saldo a favor.
    /// </summary>
    [Fact]
    public async Task SettleAsync_ASupersededOrderPaid_KeepsWhatWasPaidAsCreditBalance()
    {
        // Arrange
        var order = SubscriptionFixtures.AwaitingRenewal();
        order.Supersede(Guid.NewGuid(), TestOrders.BuyerId);
        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId);

        // Act
        await settlement.SettleAsync(order, CancellationToken.None);

        // Assert
        Assert.Equal(order.License.Total.Amount, balance.Balance.Amount);
        orders.Verify(x => x.DeactivateAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// Un pago rechazado devuelve el saldo que la orden había apartado, y una reentrega del rechazo no lo devuelve dos veces.
    /// </summary>
    [Fact]
    public async Task ReleaseAsync_ARejectedPaymentTwice_ReturnsTheReservedBalanceOnce()
    {
        // Arrange
        var withBalance = OrderAggregate.CreateRenewal(Guid.NewGuid(), Guid.NewGuid(), TestOrders.BuildLicense(), SubscriptionFixtures.Buyer(), TestOrders.BuildTenant(), SubscriptionCharge.Compute(TestOrders.BuildLicense().Total, Money.Zero("COP"), Money.FromLong(700, "COP")), TestOrders.BuyerId);

        // Act
        await settlement.ReleaseAsync(withBalance, CancellationToken.None);
        await settlement.ReleaseAsync(withBalance, CancellationToken.None);

        // Assert
        Assert.Equal(700L, balance.Balance.Amount);
    }
}
