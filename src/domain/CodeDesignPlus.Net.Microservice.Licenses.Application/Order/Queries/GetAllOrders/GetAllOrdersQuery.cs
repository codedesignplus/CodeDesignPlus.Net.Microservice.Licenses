using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetAllOrders;

public record GetAllOrdersQuery(C.Criteria Criteria) : IRequest<Pagination<OrderDto>>;
