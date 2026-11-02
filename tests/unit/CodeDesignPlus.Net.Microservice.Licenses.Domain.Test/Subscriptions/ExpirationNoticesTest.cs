using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;

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

    [Theory]
    [InlineData(30, 30)]
    [InlineData(15, 15)]
    [InlineData(7, 7)]
    [InlineData(1, 1)]
    public void Due_OnAReminderDay_IsTheUpcomingNoticeOfThatDay(int daysRemaining, int expectedDay)
    {
        // Act
        var notice = ExpirationNotices.Due(daysRemaining, Reminders, Grace, CatchUp);

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
        var notice = ExpirationNotices.Due(0, Reminders, Grace, CatchUp);

        // Assert
        Assert.Equal(new ExpirationNotice(ExpirationStage.Expired, 0), notice);
    }

    /// <summary>
    /// El día que termina la gracia toca el aviso de solo lectura.
    /// </summary>
    [Fact]
    public void Due_WhenTheGraceEnds_IsTheBlockedNotice()
    {
        // Act
        var notice = ExpirationNotices.Due(-Grace, Reminders, Grace, CatchUp);

        // Assert
        Assert.Equal(new ExpirationNotice(ExpirationStage.Blocked, -Grace), notice);
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
        var notice = ExpirationNotices.Due(daysRemaining, Reminders, Grace, CatchUp);

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
        var notice = ExpirationNotices.Due(29, Reminders, Grace, CatchUp);

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
        var notice = ExpirationNotices.Due(-60, Reminders, Grace, CatchUp);

        // Assert
        Assert.Null(notice);
    }
}
