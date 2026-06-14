using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetSubscription;

/// <summary>
/// Query para obtener la suscripción activa del tenant autenticado.
/// Busca la orden exitosa más reciente para el tenant y la licencia indicada.
/// </summary>
/// <param name="TenantId">El GUID del tenant (extraído del JWT).</param>
/// <param name="LicenseId">El GUID de la licencia asignada al tenant.</param>
public record GetSubscriptionQuery(Guid TenantId, Guid LicenseId) : IRequest<SubscriptionDto>;
