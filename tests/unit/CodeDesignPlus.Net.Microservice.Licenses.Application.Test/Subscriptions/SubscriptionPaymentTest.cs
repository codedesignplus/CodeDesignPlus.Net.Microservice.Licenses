using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.FileStorage;
using CodeDesignPlus.Net.Microservice.Emails.gRpc;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetSubscription;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;

/// <summary>
/// El pago de una renovación y lo que muestra la suscripción (pendings/735).
/// </summary>
public class SubscriptionPaymentTest
{
    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<ISubscriptionSettlement> settlement = new();

    private UpdateStateOrderCommandHandler PaymentHandler() => new(
        orders.Object, Mock.Of<IPubSub>(), Mock.Of<ILiveChannelGrpc>(), Mock.Of<IEmailGrpc>(), Mock.Of<IFileStorageGrpc>(), Mock.Of<ICurrencyGrpc>(),
        settlement.Object, Mock.Of<ILogger<UpdateStateOrderCommandHandler>>());

    /// <summary>
    /// Una renovación pagada antes de vencer empieza donde termina lo pagado, no el día del pago: la fecha de fin avanza un
    /// periodo desde la anterior.
    /// </summary>
    [Fact]
    public async Task Handle_ARenewalPaidBeforeTheEnd_StartsWhereThePaidPeriodEnds()
    {
        // Arrange
        var current = TestOrders.PaidFrom(SubscriptionFixtures.Now.Minus(Duration.FromDays(20)));
        var renewal = SubscriptionFixtures.AwaitingRenewal();
        orders.Setup(x => x.FindAsync<OrderAggregate>(renewal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(renewal);
        orders.Setup(x => x.FindPaidByTenantAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([current]);
        orders.Setup(x => x.ApplyPaymentStatusAsync(renewal, It.IsAny<PaymentStatus>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        await PaymentHandler().Handle(new UpdateStateOrderCommand(renewal.Id, PaymentStatus.Succeeded), CancellationToken.None);

        // Assert
        Assert.Equal(current.PeriodEnd, renewal.PeriodStart);
        settlement.Verify(x => x.SettleAsync(renewal, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Un pago rechazado devuelve el saldo a favor apartado y deja la licencia como estaba.
    /// </summary>
    [Fact]
    public async Task Handle_ARenewalRejected_ReleasesTheReservedBalance()
    {
        // Arrange
        var renewal = SubscriptionFixtures.AwaitingRenewal();
        orders.Setup(x => x.FindAsync<OrderAggregate>(renewal.Id, It.IsAny<CancellationToken>())).ReturnsAsync(renewal);
        orders.Setup(x => x.ApplyPaymentStatusAsync(renewal, It.IsAny<PaymentStatus>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        await PaymentHandler().Handle(new UpdateStateOrderCommand(renewal.Id, PaymentStatus.Failed), CancellationToken.None);

        // Assert
        Assert.Null(renewal.PeriodStart);
        settlement.Verify(x => x.ReleaseAsync(renewal, It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// La suscripción dice hasta cuándo está pagada, cuántos días faltan, su estado y el saldo a favor: con 20 días por
    /// delante de un periodo mensual está por vencer.
    /// </summary>
    [Fact]
    public async Task GetSubscription_TenDaysIntoAMonth_IsExpiringWithItsDaysAndCreditBalance()
    {
        // Arrange
        var current = TestOrders.PaidFrom(SubscriptionFixtures.Now.Minus(Duration.FromDays(10)));
        orders.Setup(x => x.FindPaidByTenantAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([current]);
        orders.Setup(x => x.FindAwaitingSubscriptionPaymentAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        var balance = CreditBalanceAggregate.Create(TestOrders.TenantId, "COP");
        balance.Apply(CreditMovement.ForOrder(Guid.NewGuid(), CreditMovementKind.CreditNote, Money.FromLong(1_500, "COP"), SubscriptionFixtures.Now));
        var creditBalances = new Mock<ICreditBalanceRepository>();
        creditBalances.Setup(x => x.FindAsync<CreditBalanceAggregate>(TestOrders.TenantId, TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(balance);

        var options = Microsoft.Extensions.Options.Options.Create(new SubscriptionOptions { GraceDays = 15, ReminderDays = [30, 15, 7, 1], NoticeCatchUpDays = 3 });

        // Act
        var subscription = await new GetSubscriptionQueryHandler(orders.Object, creditBalances.Object, options).Handle(new GetSubscriptionQuery(TestOrders.TenantId), CancellationToken.None);

        // Assert
        Assert.Equal(
            (SubscriptionStatus.Expiring, current.PeriodEnd!.Value, 1_500L, BillingType.Monthly, true),
            (subscription.Status, subscription.PeriodEnd, subscription.CreditBalance, subscription.BillingType, subscription.DaysRemaining is >= 17 and <= 22));
    }
}
