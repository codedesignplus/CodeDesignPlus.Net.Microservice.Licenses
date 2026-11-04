using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using LicenseStanding = CodeDesignPlus.Net.Security.Abstractions.Models.LicenseStanding;
using LicenseStatus = CodeDesignPlus.Net.Security.Abstractions.Models.LicenseStatus;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;

/// <summary>
/// Qué aviso de vencimiento le toca hoy a una suscripción (pendings/736).
/// </summary>
/// <remarks>
/// Cada aviso tiene su día: cada uno de los días previos configurados, el día del vencimiento y el día en que termina la
/// gracia. Si el job no corre ese día, el aviso sale en los días de recuperación siguientes, siempre que no le toque ya
/// otro posterior: así un corte del worker no se come un aviso, y al publicar no le llegan avisos atrasados a todas las
/// copropiedades vencidas hace meses.
/// <para>
/// El fin de la gracia no se cuenta en días de calendario: lo dice el estado de la licencia que calcula
/// <see cref="LicenseStanding.Evaluate"/>, el mismo que usa el middleware del SDK (pendings/737). Así el aviso de que
/// terminó la gracia no sale la mañana del último día, cuando la API todavía deja escribir hasta la hora en que venció.
/// </para>
/// </remarks>
public static class ExpirationNotices
{
    /// <summary>
    /// El aviso que le toca hoy, si alguno.
    /// </summary>
    /// <param name="daysRemaining">Los días de calendario que faltan para el vencimiento: cero el mismo día, negativos después.</param>
    /// <param name="standing">El estado de la licencia ahora, de <see cref="SubscriptionTimeline.Standing"/>.</param>
    /// <param name="daysSinceGraceEnded">Los días de calendario desde el día en que terminó la gracia: cero ese mismo día.</param>
    /// <param name="reminderDays">Los días previos al vencimiento en que se avisa.</param>
    /// <param name="graceDays">Los días de gracia, los de <c>Security:GraceDays</c>: dan el día del aviso de fin de la gracia.</param>
    /// <param name="catchUpDays">Cuántos días después de su día puede salir todavía un aviso que no salió.</param>
    /// <returns>La etapa y el día del aviso (los días que faltaban), o <c>null</c> si hoy no toca ninguno.</returns>
    public static ExpirationNotice? Due(int daysRemaining, LicenseStanding standing, int daysSinceGraceEnded, IReadOnlyCollection<int> reminderDays, int graceDays, int catchUpDays)
    {
        // Del más tardío al más temprano: si dos caben en la ventana, va el más reciente.
        if (standing.Status == LicenseStatus.ReadOnly)
        {
            return daysSinceGraceEnded <= catchUpDays ? new ExpirationNotice(ExpirationStage.Blocked, -graceDays) : null;
        }

        if (daysRemaining <= 0)
        {
            return daysRemaining >= -catchUpDays ? new ExpirationNotice(ExpirationStage.Expired, 0) : null;
        }

        var reminder = reminderDays
            .Where(x => x > 0 && x >= daysRemaining && x - catchUpDays <= daysRemaining)
            .OrderBy(x => x)
            .Cast<int?>()
            .FirstOrDefault();

        return reminder is null ? null : new ExpirationNotice(ExpirationStage.Upcoming, reminder.Value);
    }
}

/// <summary>
/// Un aviso de vencimiento.
/// </summary>
/// <param name="Stage">La etapa.</param>
/// <param name="Day">El día del aviso, en días que faltaban para el vencimiento (30, 15, 7, 1; 0 el día que vence; menos los días de gracia al bloquearse).</param>
public sealed record ExpirationNotice(ExpirationStage Stage, int Day);
