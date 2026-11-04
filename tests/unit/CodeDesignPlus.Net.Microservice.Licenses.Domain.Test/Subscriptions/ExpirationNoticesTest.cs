using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Security.Abstractions.Models;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test.Subscriptions;

/// <summary>
/// Qué aviso de vencimiento toca cada día (pendings/736): 30, 15, 7 y 1 día antes, el día que vence y el día que termina
/// la gracia, con recuperación de los días en que el job no corrió.
/// </summary>
public class ExpirationNoticesTest
{
    private static readonly int[] Reminders = [30, 15, 7, 1];
    private const int Grace = 15;
    private const int CatchUp = 3;
    private static readonly Instant End = Instant.FromUtc(2027, 3, 1, 15, 0);

    private static LicenseStanding ReadOnly => LicenseStanding.Evaluate(End, End.Plus(Duration.FromDays(Grace + 1)), Grace);

    /// <summary>
    /// El estado de la licencia con los días que faltan: antes del fin, activa; después, en gracia.
    /// </summary>
    private static LicenseStanding Standing(int daysRemaining) => LicenseStanding.Evaluate(End, End.Minus(Duration.FromDays(daysRemaining)).Minus(Duration.FromHours(1)), Grace);

    [Theory]
    [InlineData(30, 30)]
    [InlineData(15, 15)]
    [InlineData(7, 7)]
    [InlineData(1, 1)]
    public void Due_OnAReminderDay_IsTheUpcomingNoticeOfThatDay(int daysRemaining, int expectedDay)
    {
        // Act
        var notice = ExpirationNotices.Due(daysRemaining, Standing(daysRemaining), -Grace, Reminders, Grace, CatchUp);

        // Assert
        Assert.Equal(new ExpirationNotice(ExpirationStage.Upcoming, expectedDay), notice);
    }

    /// <summary>
    /// El día que vence toca el aviso de vencimiento, con el que empieza la gracia.
    /// </summary>
    [Fact]
    public void Due_OnTheEndDay_IsTheExpiredNotice()
    {
        // Act
        var notice = ExpirationNotices.Due(0, Standing(0), -Grace, Reminders, Grace, CatchUp);

        // Assert
        Assert.Equal(new ExpirationNotice(ExpirationStage.Expired, 0), notice);
    }

    /// <summary>
    /// Cuando la licencia queda en solo lectura toca el aviso de que terminó la gracia.
    /// </summary>
    [Fact]
    public void Due_WhenTheGraceEnds_IsTheBlockedNotice()
    {
        // Act
        var notice = ExpirationNotices.Due(-Grace, ReadOnly, 0, Reminders, Grace, CatchUp);

        // Assert
        Assert.Equal(new ExpirationNotice(ExpirationStage.Blocked, -Grace), notice);
    }

    /// <summary>
    /// El último día de gracia, antes de la hora en que vence, la licencia sigue en gracia: el aviso de que terminó no sale
    /// todavía, aunque por calendario ya sea el día (pendings/737).
    /// </summary>
    [Fact]
    public void Due_OnTheLastGraceDayBeforeTheHour_IsNothing()
    {
        // Arrange
        var standing = LicenseStanding.Evaluate(End, End.Plus(Duration.FromDays(Grace)).Minus(Duration.FromHours(4)), Grace);

        // Act
        var notice = ExpirationNotices.Due(-Grace, standing, -1, Reminders, Grace, CatchUp);

        // Assert
        Assert.Null(notice);
    }

    /// <summary>
    /// Entre un aviso y el siguiente, pasados los días de recuperación, no toca nada.
    /// </summary>
    [Theory]
    [InlineData(20)]
    [InlineData(10)]
    [InlineData(-5)]
    public void Due_BetweenNotices_IsNothing(int daysRemaining)
    {
        // Act
        var notice = ExpirationNotices.Due(daysRemaining, Standing(daysRemaining), -Grace, Reminders, Grace, CatchUp);

        // Assert
        Assert.Null(notice);
    }

    /// <summary>
    /// Si el job no corrió el día 30, el aviso sale al día siguiente: un corte del worker no se come un aviso.
    /// </summary>
    [Fact]
    public void Due_TheDayAfterAMissedReminder_StillSendsIt()
    {
        // Act
        var notice = ExpirationNotices.Due(29, Standing(29), -Grace, Reminders, Grace, CatchUp);

        // Assert
        Assert.Equal(new ExpirationNotice(ExpirationStage.Upcoming, 30), notice);
    }

    /// <summary>
    /// Una licencia vencida hace meses no recibe un aviso atrasado al publicar el job.
    /// </summary>
    [Fact]
    public void Due_LongAfterTheGraceEnded_IsNothing()
    {
        // Act
        var notice = ExpirationNotices.Due(-60, ReadOnly, 60 - Grace, Reminders, Grace, CatchUp);

        // Assert
        Assert.Null(notice);
    }
}
