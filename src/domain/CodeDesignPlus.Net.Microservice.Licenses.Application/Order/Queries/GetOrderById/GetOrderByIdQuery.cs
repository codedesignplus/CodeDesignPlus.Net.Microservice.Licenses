namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetOrderById;

public record GetOrderByIdQuery(Guid Id) : IRequest<OrderDto>;

