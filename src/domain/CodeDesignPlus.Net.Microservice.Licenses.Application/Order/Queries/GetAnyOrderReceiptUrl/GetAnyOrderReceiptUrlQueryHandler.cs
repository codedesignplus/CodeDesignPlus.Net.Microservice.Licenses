using CodeDesignPlus.Net.File.Storage.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetAnyOrderReceiptUrl;

/// <summary>
/// Handles <see cref="GetAnyOrderReceiptUrlQuery"/>.
/// </summary>
/// <remarks>
/// It does not filter by tenant on purpose: who may read every order's receipt is decided by the RBAC on its own endpoint,
/// as in <c>GET Order/all/{id}</c>. The signature goes with the order's tenant, not the caller's (<see cref="ReceiptUrl"/>).
/// </remarks>
public class GetAnyOrderReceiptUrlQueryHandler(IOrderRepository repository, IFileStorage fileStorage) : IRequestHandler<GetAnyOrderReceiptUrlQuery, string?>
{
    /// <summary>
    /// Issues a fresh signature of the order's receipt.
    /// </summary>
    /// <param name="request">The query with the order.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The signed URL, or <c>null</c> while the order has no receipt.</returns>
    public async Task<string?> Handle(GetAnyOrderReceiptUrlQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var order = await repository.FindAsync<OrderAggregate>(request.OrderId, cancellationToken);

        ApplicationGuard.IsNull(order, Errors.OrderNotFound);

        return await ReceiptUrl.SignAsync(fileStorage, order!, cancellationToken);
    }
}
