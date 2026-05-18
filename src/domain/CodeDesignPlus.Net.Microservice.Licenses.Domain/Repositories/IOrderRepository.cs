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
}
