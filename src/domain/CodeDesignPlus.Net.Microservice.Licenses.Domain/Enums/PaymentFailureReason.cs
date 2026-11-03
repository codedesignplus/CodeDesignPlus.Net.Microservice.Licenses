namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

/// <summary>
/// Por qué no se aprobó el pago de una orden, con los mismos valores con que lo publica ms-payments en
/// <c>PaymentResponseAssociatedDomainEvent.FailureReason</c> (pendings/622, 738).
/// </summary>
/// <remarks>
/// No es un catálogo: es el contrato numérico de ms-payments, que la pantalla traduce por valor.
/// </remarks>
public enum PaymentFailureReason
{
    /// <summary>
    /// Sin motivo: el pago no falló o ms-payments no lo supo.
    /// </summary>
    None = 0,

    /// <summary>
    /// El banco, la red de la tarjeta o las reglas antifraude de la pasarela lo rechazaron.
    /// </summary>
    Declined = 1,

    /// <summary>
    /// La transacción se anuló antes de liquidarse.
    /// </summary>
    Voided = 2,

    /// <summary>
    /// La pasarela no pudo procesarla por un error suyo o del procesador.
    /// </summary>
    ProviderError = 3,

    /// <summary>
    /// La sesión de pago venció antes de que se completara.
    /// </summary>
    Expired = 4
}
