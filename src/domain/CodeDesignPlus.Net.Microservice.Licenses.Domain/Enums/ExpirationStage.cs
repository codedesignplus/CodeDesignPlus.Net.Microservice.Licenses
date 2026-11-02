namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

/// <summary>
/// Qué aviso de vencimiento le toca a una suscripción (pendings/736).
/// </summary>
public enum ExpirationStage
{
    /// <summary>
    /// Faltan pocos días para el vencimiento (los días configurados en <c>Subscription:ReminderDays</c>).
    /// </summary>
    Upcoming = 0,

    /// <summary>
    /// Vence hoy: empieza la gracia.
    /// </summary>
    Expired = 1,

    /// <summary>
    /// Terminó la gracia sin renovar.
    /// </summary>
    Blocked = 2
}
