using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// Lo que dice el aviso de una orden pagada que no se pudo aplicar (Kappali pendings/737, paso 6).
/// </summary>
/// <remarks>
/// Es la reserva de la campana para cuando el frontend no conoce el tipo de aviso: el frontend pinta el suyo con el
/// payload. Habla de la orden y del tenant, sin lenguaje de ningún producto, porque este servicio es transversal.
/// </remarks>
public static class OrderNotAppliedText
{
    /// <summary>
    /// El título del aviso.
    /// </summary>
    public const string Title = "Una orden pagada no se aplicó";

    /// <summary>
    /// El módulo dueño del dato al que lleva el aviso: el detalle de la orden.
    /// </summary>
    public const string Module = "orders";

    /// <summary>
    /// Para qué era la orden, como se lee en el aviso.
    /// </summary>
    /// <param name="type">El tipo de la orden.</param>
    /// <returns>El nombre del tipo.</returns>
    public static string TypeName(OrderType type) => type switch
    {
        OrderType.Renewal => "renovación",
        OrderType.Change => "cambio de licencia",
        OrderType.Package => "paquete",
        _ => "compra",
    };

    /// <summary>
    /// El texto del aviso con la orden.
    /// </summary>
    /// <param name="tenantName">El nombre del tenant de la orden.</param>
    /// <param name="type">El tipo de la orden.</param>
    /// <param name="stepName">El paso que falló.</param>
    /// <param name="reason">Por qué falló.</param>
    /// <returns>El texto.</returns>
    /// <remarks>
    /// No lleva el monto: el importe lo formatea el frontend con su moneda y el locale del usuario desde el payload
    /// (<c>amountPaid</c> en unidad menor y <c>currency</c>), y ya lo pinta en su propia línea.
    /// </remarks>
    public static string Message(string tenantName, OrderType type, string stepName, string reason) =>
        $"La orden de {TypeName(type)} de {tenantName} se pagó, pero no se aplicó (paso {stepName}): {reason.TrimEnd('.', ' ')}. Revisa la orden.";

    /// <summary>
    /// El texto del aviso cuando la orden no se encuentra: dice lo que trae el evento.
    /// </summary>
    /// <param name="orderId">La orden.</param>
    /// <param name="stepName">El paso que falló.</param>
    /// <param name="reason">Por qué falló.</param>
    /// <returns>El texto.</returns>
    public static string MessageWithoutOrder(Guid orderId, string stepName, string reason) =>
        $"La orden {orderId} se pagó, pero no se aplicó (paso {stepName}): {reason.TrimEnd('.', ' ')}. Revisa la orden.";
}
