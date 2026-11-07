using CodeDesignPlus.Net.File.Storage.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetOrderReceiptUrl;

/// <summary>
/// Manejador de <see cref="GetOrderReceiptUrlQuery"/>.
/// </summary>
/// <remarks>
/// Devuelve <c>null</c> cuando la orden todavia no tiene recibo, que es un estado legitimo: el PDF se genera
/// despues de que la pasarela confirma el pago. La pantalla oculta el boton en ese caso en vez de mostrar
/// uno roto. Solo firma el recibo de una orden de la copropiedad de la sesión o que creó el usuario.
/// </remarks>
public class GetOrderReceiptUrlQueryHandler(IOrderRepository repository, IFileStorage fileStorage, IUserContext user)
    : IRequestHandler<GetOrderReceiptUrlQuery, string?>
{
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

        // Como GET Order/{id}: el recibo de otra copropiedad responde lo mismo que una orden que no existe (pendings/735).
        ApplicationGuard.IsFalse(order?.IsVisibleTo(user.Tenant, user.IdUser) == true, Errors.OrderNotFound);

        return await ReceiptUrl.SignAsync(fileStorage, order!, cancellationToken);
    }
}
