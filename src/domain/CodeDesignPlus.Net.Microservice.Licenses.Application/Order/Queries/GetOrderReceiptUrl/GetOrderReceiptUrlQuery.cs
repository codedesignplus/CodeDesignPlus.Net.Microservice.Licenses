namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetOrderReceiptUrl;

/// <summary>
/// Devuelve una URL firmada, recien emitida, del PDF del recibo de compra.
/// </summary>
/// <remarks>
/// Es una consulta y no un campo del <c>OrderDto</c> a proposito: la firma de ms-filestorage caduca, asi que
/// una URL persistida o cacheada seria un enlace roto con fecha. La orden guarda la referencia; la firma se
/// pide en el momento de mostrarla.
/// </remarks>
public record GetOrderReceiptUrlQuery(Guid OrderId) : IRequest<string?>;

/// <summary>
/// Validador de <see cref="GetOrderReceiptUrlQuery"/>.
/// </summary>
public class Validator : AbstractValidator<GetOrderReceiptUrlQuery>
{
    /// <summary>
    /// Inicializa las reglas de validacion de la consulta.
    /// </summary>
    public Validator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
