using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Core.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Jobs;
using CodeDesignPlus.Net.Microservice.Licenses.Domain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;
using CodeDesignPlus.Net.PubSub.Abstractions;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Test.Jobs;

/// <summary>
/// El job de avisos de vencimiento (pendings/736): las etapas, el día en el calendario de la copropiedad y que correr dos
/// veces no repite el aviso.
/// </summary>
public class SubscriptionExpirationJobTest
{
    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<ISubscriptionNoticeRepository> notices = new();
    private readonly Mock<IPubSub> pubsub = new();
    private readonly List<SubscriptionExpiringDomainEvent> published = [];
    private readonly HashSet<Guid> stored = [];

    public SubscriptionExpirationJobTest()
    {
        notices.Setup(x => x.FindUnpublishedAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);
        notices.Setup(x => x.CreateIfAbsentAsync(It.IsAny<SubscriptionNoticeAggregate>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((SubscriptionNoticeAggregate notice, CancellationToken _) => stored.Add(notice.Id));
        pubsub.Setup(x => x.PublishAsync(It.IsAny<IReadOnlyList<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<IDomainEvent>, CancellationToken>((events, _) => published.AddRange(events.OfType<SubscriptionExpiringDomainEvent>()))
            .Returns(Task.CompletedTask);
        pubsub.Setup(x => x.PublishAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IDomainEvent, CancellationToken>((@event, _) => published.AddRange(new[] { @event }.OfType<SubscriptionExpiringDomainEvent>()))
            .Returns(Task.CompletedTask);
    }

    private SubscriptionExpirationJob Job() => new(
        orders.Object, notices.Object, pubsub.Object,
        Microsoft.Extensions.Options.Options.Create(new SubscriptionOptions { GraceDays = 15, ReminderDays = [30, 15, 7, 1], NoticeCatchUpDays = 3 }),
        new Mock<ILogger<SubscriptionExpirationJob>>().Object);

    /// <summary>
    /// A las 6 a. m. de Bogotá del día del aviso.
    /// </summary>
    private static Instant MorningOf(Instant end, int daysBefore) => end.InZone(DateTimeZoneProviders.Tzdb["America/Bogota"]).Date.PlusDays(-daysBefore).At(new LocalTime(6, 0)).InZoneStrictly(DateTimeZoneProviders.Tzdb["America/Bogota"]).ToInstant();

    [Theory]
    [InlineData(30, "Upcoming")]
    [InlineData(15, "Upcoming")]
    [InlineData(7, "Upcoming")]
    [InlineData(1, "Upcoming")]
    [InlineData(0, "Expired")]
    [InlineData(-15, "Blocked")]
    public async Task RunAsync_OnTheDayOfEachStage_PublishesThatStage(int daysBefore, string stage)
    {
        // Arrange
        var order = TestOrders.PaidFrom(Instant.FromUtc(2027, 3, 1, 15, 0));
        orders.Setup(x => x.FindAllPaidAsync(It.IsAny<CancellationToken>())).ReturnsAsync([order]);

        // Act
        await Job().RunAsync(MorningOf(order.PeriodEnd!.Value, daysBefore), CancellationToken.None);

        // Assert
        var @event = Assert.Single(published);
        Assert.Equal((stage, daysBefore, TestOrders.TenantId), (@event.Stage, @event.DaysRemaining, @event.Tenant));
    }

    /// <summary>
    /// El job que corre dos veces el mismo día encuentra la constancia del aviso y no lo repite.
    /// </summary>
    [Fact]
    public async Task RunAsync_TwiceTheSameDay_PublishesTheNoticeOnce()
    {
        // Arrange
        var order = TestOrders.PaidFrom(Instant.FromUtc(2027, 3, 1, 15, 0));
        orders.Setup(x => x.FindAllPaidAsync(It.IsAny<CancellationToken>())).ReturnsAsync([order]);
        var now = MorningOf(order.PeriodEnd!.Value, 7);

        // Act
        await Job().RunAsync(now, CancellationToken.None);
        await Job().RunAsync(now.Plus(Duration.FromHours(3)), CancellationToken.None);

        // Assert
        Assert.Single(published);
    }

    /// <summary>
    /// Una renovación pagada mueve el fin del periodo: el aviso del vencimiento anterior ya no toca.
    /// </summary>
    [Fact]
    public async Task RunAsync_AfterARenewalPaid_DoesNotWarnAboutThePreviousEnd()
    {
        // Arrange
        var order = TestOrders.PaidFrom(Instant.FromUtc(2027, 3, 1, 15, 0));
        var renewal = TestOrders.PaidFrom(order.PeriodEnd!.Value);
        orders.Setup(x => x.FindAllPaidAsync(It.IsAny<CancellationToken>())).ReturnsAsync([order, renewal]);

        // Act
        await Job().RunAsync(MorningOf(order.PeriodEnd!.Value, 7), CancellationToken.None);

        // Assert
        Assert.Empty(published);
    }

    /// <summary>
    /// Un aviso guardado cuyo evento no salió se publica otra vez con el mismo identificador, para que quien avisa no lo
    /// repita si el anterior sí había llegado.
    /// </summary>
    [Fact]
    public async Task RunAsync_ANoticeNotPublished_IsRepublishedWithTheSameEventId()
    {
        // Arrange
        var order = TestOrders.PaidFrom(Instant.FromUtc(2027, 3, 1, 15, 0));
        var subscription = Domain.ServiceDomain.SubscriptionTimeline.From([order])!;
        var pending = SubscriptionNoticeAggregate.Create(subscription, new Domain.ServiceDomain.ExpirationNotice(Domain.Enums.ExpirationStage.Upcoming, 7), MorningOf(order.PeriodEnd!.Value, 7), 15, Guid.Empty);
        notices.Setup(x => x.FindUnpublishedAsync(It.IsAny<CancellationToken>())).ReturnsAsync([pending]);
        orders.Setup(x => x.FindAllPaidAsync(It.IsAny<CancellationToken>())).ReturnsAsync([]);

        // Act
        await Job().RunAsync(MorningOf(order.PeriodEnd!.Value, 6), CancellationToken.None);

        // Assert
        Assert.Equal(pending.Id, Assert.Single(published).EventId);
        notices.Verify(x => x.MarkPublishedAsync(pending.Id, It.IsAny<Instant>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Una copropiedad que falla no impide avisar a las demás, pero el job termina en error para que se vea en Hangfire.
    /// </summary>
    [Fact]
    public async Task RunAsync_OneTenantFails_NotifiesTheOthersAndThenFails()
    {
        // Arrange
        var order = TestOrders.PaidFrom(Instant.FromUtc(2027, 3, 1, 15, 0));
        orders.Setup(x => x.FindAllPaidAsync(It.IsAny<CancellationToken>())).ReturnsAsync([order]);
        notices.SetupSequence(x => x.CreateIfAbsentAsync(It.IsAny<SubscriptionNoticeAggregate>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Mongo is down"));

        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => Job().RunAsync(MorningOf(order.PeriodEnd!.Value, 7), CancellationToken.None));

        // Assert
        Assert.Contains("1 tenant", exception.Message);
    }
}
