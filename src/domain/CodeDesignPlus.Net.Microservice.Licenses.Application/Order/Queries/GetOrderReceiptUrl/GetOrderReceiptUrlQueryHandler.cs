using CodeDesignPlus.Net.File.Storage.Abstractions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetOrderReceiptUrl;

/// <summary>
/// Manejador de <see cref="GetOrderReceiptUrlQuery"/>.
/// </summary>
/// <remarks>
/// Devuelve <c>null</c> cuando la orden todavia no tiene recibo, que es un estado legitimo: el PDF se genera
/// despues de que la pasarela confirma el pago. La pantalla oculta el boton en ese caso en vez de mostrar
/// uno roto.
/// </remarks>
public class GetOrderReceiptUrlQueryHandler(IOrderRepository repository, IFileStorage fileStorage)
    : IRequestHandler<GetOrderReceiptUrlQuery, string?>
{
    /// <summary>La firma vive lo justo para abrir el PDF; no se guarda ni se cachea.</summary>
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Emite una firma nueva para el recibo de la orden.
    /// </summary>
    /// <param name="request">La consulta con el identificador de la orden.</param>
    /// <param name="cancellationToken">Token para monitorear solicitudes de cancelacion.</param>
    /// <returns>La URL firmada, o <c>null</c> si la orden todavia no tiene recibo.</returns>
    public async Task<string?> Handle(GetOrderReceiptUrlQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var order = await repository.FindAsync<OrderAggregate>(request.OrderId, cancellationToken);

        ApplicationGuard.IsNull(order, Errors.OrderNotFound);

        if (order.Receipt is null)
            return null;

        var response = await fileStorage.GetSignedUrlAsync(
            order.Receipt.Name, order.Receipt.Target, Lifetime, order.TenantDetail.Id, cancellationToken);

        return response?.Success == true ? response.File.Detail.SignedUrl.ToString() : null;
    }
}
