using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Hangfire.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Jobs;
using CodeDesignPlus.Net.Microservice.Licenses.Domain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;
using Hangfire;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Test.Jobs;

/// <summary>
/// The job ends the wait of the email for the invoice: it sends what is due of each overdue order and fails the round when
/// one could not be sent (pendings/661, step 4; rule 23).
/// </summary>
public class PurchaseEmailJobTest
{
    private static readonly Instant Now = Instant.FromUtc(2026, 11, 10, 16, 0);

    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<IPurchaseEmails> emails = new();

    private PurchaseEmailJob Job() => new(orders.Object, emails.Object, new FixedClock(Now), Mock.Of<ILogger<PurchaseEmailJob>>());

    private static IJobCancellationToken Token()
    {
        var token = new Mock<IJobCancellationToken>();

        token.Setup(x => x.ShutdownToken).Returns(CancellationToken.None);

        return token.Object;
    }

    [Fact]
    public async Task ExecuteAsync_OverdueOrders_SendsWhatIsDueOfEach()
    {
        // Arrange
        var first = TestOrders.Build();
        var second = TestOrders.Build();

        orders.Setup(x => x.FindReceiptEmailsOverdueAsync(Now, PurchaseEmailJob.BatchSize, It.IsAny<CancellationToken>())).ReturnsAsync([first, second]);

        // Act
        await Job().ExecuteAsync(Token());

        // Assert
        emails.Verify(x => x.SendDueAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task ExecuteAsync_OneFails_SendsTheOthersAndFailsTheRound()
    {
        // Arrange
        var failing = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid(), TestOrders.BuildLicense(), TestOrders.Build().Buyer, TestOrders.BuildTenant(), TestOrders.BuyerId);
        var other = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid(), TestOrders.BuildLicense(), TestOrders.Build().Buyer, TestOrders.BuildTenant(), TestOrders.BuyerId);

        orders.Setup(x => x.FindReceiptEmailsOverdueAsync(Now, PurchaseEmailJob.BatchSize, It.IsAny<CancellationToken>())).ReturnsAsync([failing, other]);
        emails.Setup(x => x.SendDueAsync(failing.Id, It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("bus down"));

        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => Job().ExecuteAsync(Token()));

        // Assert
        emails.Verify(x => x.SendDueAsync(other.Id, It.IsAny<CancellationToken>()), Times.Once);
    }

    private sealed class FixedClock(Instant now) : IClock
    {
        public Instant GetCurrentInstant() => now;
    }
}
