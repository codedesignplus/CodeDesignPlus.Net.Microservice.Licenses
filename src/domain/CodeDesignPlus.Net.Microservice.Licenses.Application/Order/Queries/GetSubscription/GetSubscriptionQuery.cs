using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetSubscription;

/// <summary>
/// Query para obtener la suscripción de la copropiedad autenticada (pendings/735).
/// </summary>
/// <param name="TenantId">El GUID del tenant (extraído del JWT).</param>
public record GetSubscriptionQuery(Guid TenantId) : IRequest<SubscriptionDto>;
