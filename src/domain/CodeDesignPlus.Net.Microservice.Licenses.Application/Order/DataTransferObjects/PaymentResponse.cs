using System;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

/// <summary>
/// Enum para el tipo de acción que el sistema debe tomar después de iniciar un pago.
/// </summary>
public enum NextActionType
{
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
}
