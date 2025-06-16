namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetOrderById;

public class GetOrderByIdQueryHandler(IOrderRepository repository, IMapper mapper, IUserContext user) : IRequestHandler<GetOrderByIdQuery, OrderDto>
{
    public async Task<OrderDto> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var order = await repository.FindAsync<OrderAggregate>(request.Id, user.Tenant, cancellationToken);

        ApplicationGuard.IsNull(order, Errors.OrderNotFound);

        return mapper.Map<OrderDto>(order);
    }
}
