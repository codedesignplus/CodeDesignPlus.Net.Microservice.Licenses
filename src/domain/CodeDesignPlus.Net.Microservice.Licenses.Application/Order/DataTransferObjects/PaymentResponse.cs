using System;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

/// <summary>
/// Enum para el tipo de acción que el sistema debe tomar después de iniciar un pago.
/// </summary>
public enum NextActionType
{
    /// <summary>
    /// No se requiere ninguna acción adicional.
    /// </summary>
    None,
    /// <summary>
    /// Redirigir al usuario a una URL específica proporcionada por el proveedor de pagos.
    /// </summary>
    Redirect,
    /// <summary>
    /// El frontend debe mostrar un widget de pago integrado proporcionado por el proveedor de pagos.
    /// </summary>
    DisplayWidget,
    /// <summary>
    /// Esperar una confirmación externa (por ejemplo, un webhook) antes de proceder.
    /// </summary>
    WaitConfirmation
}

/// <summary>
/// DTO que representa la respuesta después de iniciar un pago.
/// </summary>
public class PaymentResponse
{
    /// <summary>
    /// La orden cuyo pago se inició. Si se reutilizó una renovación o un cambio que ya esperaba su pago, es esa orden y no
    /// la del id enviado (pendings/735): la pantalla espera el desenlace leyendo <c>GET Order/{id}</c> con este id.
    /// </summary>
    public Guid Id { get; set; }
    /// <summary>
    /// Indica si la iniciación del pago fue exitosa.
    /// </summary>
    public bool Success { get; set; }
    /// <summary>
    /// El ID del pago en nuestro sistema.
    /// </summary>
    public Guid PaymentId { get; set; }
    /// <summary>
    /// La siguiente acción que el sistema debe tomar.
    /// </summary>
    public NextActionType NextAction { get; set; }
    /// <summary>
    /// URL de redirección proporcionada por el proveedor de pagos.
    /// </summary>
    public string? RedirectUrl { get; set; }
    /// <summary>
    /// Metadatos adicionales proporcionados por el proveedor de pagos.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = [];
    /// <summary>
    /// Si la orden quedó pagada sin pasar por la pasarela: el crédito y el saldo a favor cubrieron todo el precio
    /// (pendings/735, 738). Con <c>true</c> no hay a dónde redirigir y <see cref="NextAction"/> es <c>None</c>.
    /// </summary>
    public bool ConfirmedWithoutPayment { get; set; }
}
