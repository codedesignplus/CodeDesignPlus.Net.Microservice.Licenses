using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetAnyOrderById;

/// <summary>
/// Manejador de <see cref="GetAnyOrderByIdQuery"/>.
/// </summary>
/// <remarks>
/// No filtra por copropiedad a propósito: quién puede leer todas las órdenes lo decide el RBAC sobre su propio endpoint,
/// como en <c>GET Order/all</c>. El micro no comprueba cargos.
/// </remarks>
public class GetAnyOrderByIdQueryHandler(IOrderRepository repository, IMapper mapper) : IRequestHandler<GetAnyOrderByIdQuery, OrderDto>
{
    public async Task<OrderDto> Handle(GetAnyOrderByIdQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var order = await repository.FindAsync<OrderAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsNull(order, Errors.OrderNotFound);

        return mapper.Map<OrderDto>(order);
    }
}
