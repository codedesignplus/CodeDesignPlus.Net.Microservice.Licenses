using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using static CodeDesignPlus.Net.Microservice.Licenses.Domain.Test.Subscriptions.SubscriptionOrders;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test.Subscriptions;

/// <summary>
/// La suscripción armada con las órdenes pagadas (pendings/735): hasta cuándo está pagada, cuántos días faltan en el
/// calendario de la copropiedad y en qué estado está.
/// </summary>
public class SubscriptionTimelineTest
{
    private const int Window = 30;
    private const int Grace = 15;

    /// <summary>
    /// Mensual es un mes de calendario: un periodo que empieza el 31 de enero termina el 28 de febrero.
    /// </summary>
    [Fact]
    public void BillingPeriod_Monthly_EndsOneCalendarMonthLater()
    {
        // Act
        var end = BillingPeriod.End(Instant.FromUtc(2027, 1, 31, 15, 0), BillingType.Monthly);

        // Assert
        Assert.Equal(Instant.FromUtc(2027, 2, 28, 15, 0), end);
    }

    /// <summary>
    /// Una renovación pagada por adelantado alarga la suscripción: está pagada hasta el fin de la renovación, desde el
    /// inicio del primer periodo.
    /// </summary>
    [Fact]
    public void PaidThrough_WithARenewalPaidInAdvance_IsTheEndOfTheRenewal()
    {
        // Arrange
        var current = Paid(Bogota(2027, 3, 1, 10));
        var renewal = PaidRenewal(current.PeriodEnd!.Value);

        // Act
        var subscription = SubscriptionTimeline.From([renewal, current])!;

        // Assert
        Assert.Equal((renewal.PeriodEnd!.Value, current.PeriodStart!.Value), (subscription.PaidThrough, subscription.CoverageStart));
    }

    /// <summary>
    /// Una orden inactiva (acreditada por un cambio) o sin pagar no cuenta.
    /// </summary>
    [Fact]
    public void From_WithoutPaidOrders_IsNull()
    {
        // Arrange
        var unpaid = OrdenDePrueba.Nueva();

        // Act
        var subscription = SubscriptionTimeline.From([unpaid]);

        // Assert
        Assert.Null(subscription);
    }

    /// <summary>
    /// Los días se cuentan en el calendario de Bogotá: a las 8 p. m. del 27 de marzo en Bogotá ya es 28 en UTC, pero para
    /// la copropiedad falta un día para el vencimiento del 28.
    /// </summary>
    [Fact]
    public void DaysRemaining_LateInTheEveningInBogota_CountsTheLocalDay()
    {
        // Arrange
        var subscription = SubscriptionTimeline.From([Paid(Bogota(2027, 2, 28, 10))])!;

        // Act
        var days = subscription.DaysRemaining(Bogota(2027, 3, 27, 20));

        // Assert
        Assert.Equal(1, days);
    }

    [Theory]
    [InlineData(31, SubscriptionStatus.Active)]
    [InlineData(30, SubscriptionStatus.Expiring)]
    [InlineData(1, SubscriptionStatus.Expiring)]
    public void StatusAt_BeforeTheEnd_IsActiveOrExpiringByTheWindow(int daysBefore, SubscriptionStatus expected)
    {
        // Arrange
        var subscription = SubscriptionTimeline.From([Paid(Bogota(2027, 3, 1, 10), license: Annual())])!;
        var now = subscription.PaidThrough.Minus(Duration.FromDays(daysBefore));

        // Act
        var status = subscription.StatusAt(now, Window, Grace);

        // Assert
        Assert.Equal(expected, status);
    }

    /// <summary>
    /// Vencida, dentro de los días de gracia, está en gracia; pasada la gracia, vencida.
    /// </summary>
    [Theory]
    [InlineData(0, SubscriptionStatus.Grace)]
    [InlineData(14, SubscriptionStatus.Grace)]
    [InlineData(15, SubscriptionStatus.Expired)]
    public void StatusAt_AfterTheEnd_IsGraceThenExpired(int daysAfter, SubscriptionStatus expected)
    {
        // Arrange
        var subscription = SubscriptionTimeline.From([Paid(Bogota(2027, 3, 1, 10))])!;
        var now = subscription.PaidThrough.Plus(Duration.FromDays(daysAfter));

        // Act
        var status = subscription.StatusAt(now, Window, Grace);

        // Assert
        Assert.Equal(expected, status);
    }

    private static ValueObjects.License Annual() => ValueObjects.License.Create(
        Guid.NewGuid(), "Esencial", Money.FromLong(100, "COP"), Money.FromLong(0, "COP"), Money.FromLong(100, "COP"),
        BillingType.Annually, BillingModel.FlatRate, "Plan", "Plan", ValueObjects.Icon.Create("solar:home-2-linear", "#2563eb"), "Terms", false, true, [], []);
}
