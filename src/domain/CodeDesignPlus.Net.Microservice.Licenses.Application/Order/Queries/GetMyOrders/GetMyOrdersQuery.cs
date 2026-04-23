using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetMyOrders;

public record GetMyOrdersQuery(C.Criteria Criteria) : IRequest<Pagination<OrderDto>>;

