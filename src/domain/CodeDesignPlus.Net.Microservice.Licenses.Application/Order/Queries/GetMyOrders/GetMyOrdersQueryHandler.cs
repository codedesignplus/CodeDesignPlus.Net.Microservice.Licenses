using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetMyOrders;

public class GetMyOrdersQueryHandler(IOrderRepository repository, IMapper mapper, IUserContext user) 
    : IRequestHandler<GetMyOrdersQuery, Pagination<OrderDto>>
{
    public async Task<Pagination<OrderDto>> Handle(GetMyOrdersQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        if (string.IsNullOrEmpty(request.Criteria.Filters))
            request.Criteria.Filters = $"CreatedBy={user.IdUser}";

        if (!request.Criteria.Filters.Contains("CreatedBy"))
            request.Criteria.Filters += $"|and|CreatedBy={user.IdUser}";

        var orders = await repository.MatchingAsync<OrderAggregate>(request.Criteria, cancellationToken);

        var ordersDto = mapper.Map<Pagination<OrderDto>>(orders);

        return ordersDto;
    }
}
