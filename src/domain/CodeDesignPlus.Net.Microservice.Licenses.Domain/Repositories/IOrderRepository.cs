using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

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
}
