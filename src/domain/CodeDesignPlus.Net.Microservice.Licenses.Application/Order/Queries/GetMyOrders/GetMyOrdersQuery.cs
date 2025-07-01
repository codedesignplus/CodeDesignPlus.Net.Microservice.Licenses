using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetMyOrders;

public record GetMyOrdersQuery(C.Criteria Criteria) : IRequest<Pagination<OrderDto>>;

