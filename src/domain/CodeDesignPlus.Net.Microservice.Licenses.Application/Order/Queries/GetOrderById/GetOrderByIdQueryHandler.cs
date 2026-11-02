using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetOrderById;

/// <summary>
/// Lee una orden de la copropiedad de la sesión, o una que creó el usuario (pendings/735).
/// </summary>
/// <remarks>
/// La orden de otra copropiedad responde el mismo «orden no encontrada» que una que no existe, para que su id no revele
/// si existe. La plataforma lee cualquier orden con <see cref="GetAnyOrderById.GetAnyOrderByIdQuery"/>.
/// </remarks>
public class GetOrderByIdQueryHandler(IOrderRepository repository, IMapper mapper, IUserContext user) : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    public async Task<OrderDto> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var order = await repository.FindAsync<OrderAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsFalse(order?.IsVisibleTo(user.Tenant, user.IdUser) == true, Errors.OrderNotFound);

        return mapper.Map<OrderDto>(order);
    }
}
