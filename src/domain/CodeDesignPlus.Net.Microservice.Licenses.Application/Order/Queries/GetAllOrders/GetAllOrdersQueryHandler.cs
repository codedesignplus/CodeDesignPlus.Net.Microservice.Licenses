using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetAllOrders;

public class GetAllOrdersQueryHandler(IOrderRepository repository, IMapper mapper)
    : IRequestHandler<GetAllOrdersQuery, Pagination<OrderDto>>
{
    public async Task<Pagination<OrderDto>> Handle(GetAllOrdersQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var orders = await repository.GetAllOrdersAsync(request.Criteria, cancellationToken);

        return mapper.Map<Pagination<OrderDto>>(orders);
    }
}
