namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

/// <summary>
/// Por qué se movió el saldo a favor de una copropiedad (pendings/738).
/// </summary>
public enum CreditMovementKind
{
    /// <summary>
    /// Entra: lo no prestado de la licencia anterior al cambiar de licencia (la nota crédito).
    /// </summary>
    CreditNote = 0,

    /// <summary>
    /// Sale: el saldo que se aplica como anticipo a una orden.
    /// </summary>
    AppliedToOrder = 1,

    /// <summary>
    /// Entra: el saldo apartado para una orden que no se pagó vuelve a estar disponible.
    /// </summary>
    Released = 2,

    /// <summary>
    /// Entra: lo pagado por una orden que otra ya había reemplazado, y que por eso no se aplica a la licencia.
    /// </summary>
    UnappliedPayment = 3
}
