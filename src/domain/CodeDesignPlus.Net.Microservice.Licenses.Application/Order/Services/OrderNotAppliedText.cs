using System.Net;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// Lo que dicen el aviso y el correo de una orden pagada que no se pudo aplicar (Kappali pendings/737, paso 6).
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

    /// <summary>
    /// La plantilla de ms-emails del correo al buzón de operaciones: la genérica de avisos, con asunto, título y contenido.
    /// </summary>
    public const string EmailTemplate = "Notice";

    /// <summary>
    /// Cómo empieza el asunto del correo, para que se distinga en la bandeja del buzón de operaciones.
    /// </summary>
    public const string ImportantPrefix = "IMPORTANTE:";

    /// <summary>
    /// El asunto del correo al buzón de operaciones.
    /// </summary>
    /// <param name="about">De quién es la orden: el nombre del tenant o, si la orden no aparece, su id.</param>
    /// <returns>El asunto, empezando por <see cref="ImportantPrefix"/>.</returns>
    public static string EmailSubject(string about) => $"{ImportantPrefix} orden pagada sin aplicar — {about}";

    /// <summary>
    /// El contenido HTML del correo con la orden: el tenant, el tipo, la orden, lo pagado, el paso y el motivo.
    /// </summary>
    /// <param name="tenantName">El nombre del tenant de la orden.</param>
    /// <param name="type">El tipo de la orden.</param>
    /// <param name="orderId">La orden.</param>
    /// <param name="amountPaid">Lo pagado, ya formateado como en los demás correos de este servicio.</param>
    /// <param name="stepName">El paso que falló.</param>
    /// <param name="reason">Por qué falló.</param>
    /// <returns>El HTML del contenido, con cada valor escapado.</returns>
    public static string EmailContent(string tenantName, OrderType type, Guid orderId, string amountPaid, string stepName, string reason) =>
        Paragraph($"La orden de {TypeName(type)} de {tenantName} se pagó, pero no se aplicó.")
        + List(
            ("Organización", tenantName),
            ("Tipo de orden", Capitalize(TypeName(type))),
            ("Orden", orderId.ToString()),
            ("Monto pagado", amountPaid),
            ("Paso", stepName),
            ("Motivo", reason))
        + Paragraph("Revisa la orden en la plataforma.");

    /// <summary>
    /// El contenido HTML del correo cuando la orden no se encuentra: dice lo que trae el evento.
    /// </summary>
    /// <param name="orderId">La orden.</param>
    /// <param name="stepName">El paso que falló.</param>
    /// <param name="reason">Por qué falló.</param>
    /// <returns>El HTML del contenido, con cada valor escapado.</returns>
    public static string EmailContentWithoutOrder(Guid orderId, string stepName, string reason) =>
        Paragraph($"La orden {orderId} se pagó, pero no se aplicó, y no se encontró para leer la organización ni lo pagado.")
        + List(
            ("Orden", orderId.ToString()),
            ("Paso", stepName),
            ("Motivo", reason))
        + Paragraph("Revisa la orden en la plataforma.");

    private static string Paragraph(string text) => $"<p>{WebUtility.HtmlEncode(text)}</p>";

    private static string List(params (string Label, string Value)[] items) =>
        $"<ul>{string.Concat(items.Select(x => $"<li><strong>{WebUtility.HtmlEncode(x.Label)}:</strong> {WebUtility.HtmlEncode(x.Value)}</li>"))}</ul>";

    private static string Capitalize(string text) => string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text[1..];
}
