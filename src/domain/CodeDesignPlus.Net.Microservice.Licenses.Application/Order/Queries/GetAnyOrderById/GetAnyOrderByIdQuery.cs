using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetAnyOrderById;

/// <summary>
/// Lee cualquier orden, sea de la copropiedad que sea: la vista de plataforma de las órdenes, como <c>GET Order/all</c>
/// (pendings/735).
/// </summary>
/// <param name="Id">La orden.</param>
public record GetAnyOrderByIdQuery(Guid Id) : IRequest<OrderDto>;
