namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

/// <summary>
/// En qué punto de su vigencia está la suscripción de una copropiedad (pendings/735, 737).
/// </summary>
public enum SubscriptionStatus
{
    /// <summary>
    /// Vigente y lejos del vencimiento.
    /// </summary>
    Active = 0,

    /// <summary>
    /// Vigente, pero dentro de la ventana de avisos previos al vencimiento.
    /// </summary>
    Expiring = 1,

    /// <summary>
    /// Vencida, dentro de los días de gracia: todo sigue funcionando.
    /// </summary>
    Grace = 2,

    /// <summary>
    /// Vencida y pasada la gracia.
    /// </summary>
    Expired = 3
}
