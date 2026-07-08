using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using C = CodeDesignPlus.Net.Core.Abstractions.Models;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;

/// <summary>
/// Define el contrato del repositorio para la persistencia y recuperación del agregado
/// <see cref="OrderAggregate"/>.
/// </summary>
public interface IOrderRepository : IRepositoryBase
{
    /// <summary>
    /// Busca la orden exitosa más reciente para un tenant dado.
    /// Utilizado por el servicio gRPC para obtener el snapshot de licencia al provisionar
    /// o para validar la licencia activa en el middleware de seguridad.
    /// </summary>
    /// <param name="tenantId">El identificador del tenant.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La orden exitosa más reciente, o null si no existe.</returns>
    Task<OrderAggregate?> FindSucceededOrderByTenantAsync(Guid tenantId, CancellationToken cancellationToken);

    /// <summary>
    /// Busca la orden exitosa activa para un tenant y una licencia específica.
    /// Retorna la más reciente si existen múltiples órdenes (upgrades/renovaciones).
    /// </summary>
    /// <param name="tenantId">El identificador del tenant.</param>
    /// <param name="licenseId">El identificador de la licencia.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La orden exitosa más reciente para la combinación tenant+licencia, o null si no existe.</returns>
    Task<OrderAggregate?> FindActiveSubscriptionAsync(Guid tenantId, Guid licenseId, CancellationToken cancellationToken);

    /// <summary>
    /// Completa un paso de provisioning de forma atómica usando FindOneAndUpdate.
    /// Evita race conditions cuando múltiples handlers actualizan la misma orden concurrentemente.
    /// </summary>
    Task<OrderAggregate?> CompleteProvisioningStepAtomicAsync(Guid orderId, string stepName, CancellationToken cancellationToken);

    /// <summary>
    /// Finds orders stuck in a given provisioning status for longer than the specified duration.
    /// Used by the reconciliation job to detect and recover failed provisioning flows.
    /// </summary>
    /// <param name="status">The provisioning status to filter by.</param>
    /// <param name="stuckDuration">The minimum time the order must have been stuck.</param>
    /// <param name="limit">The maximum number of orders to return.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>A list of stuck orders ordered by UpdatedAt ascending.</returns>
    Task<List<OrderAggregate>> FindStuckOrdersAsync(ProvisioningStatus status, Duration stuckDuration, int limit, CancellationToken cancellationToken);

    /// <summary>
    /// Retrieves all orders across all tenants with criteria-based filtering, sorting, and pagination.
    /// Bypasses tenant isolation for administrative views.
    /// </summary>
    /// <param name="criteria">The criteria for filtering, sorting, and paginating orders.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>A paginated list of orders.</returns>
    Task<Pagination<OrderAggregate>> GetAllOrdersAsync(C.Criteria criteria, CancellationToken cancellationToken);
}
