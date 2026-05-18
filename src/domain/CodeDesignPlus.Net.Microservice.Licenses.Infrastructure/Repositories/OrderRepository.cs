using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Repositories;

/// <summary>
/// Implementación del repositorio de órdenes usando MongoDB.
/// </summary>
public class OrderRepository(IServiceProvider serviceProvider, IOptions<MongoOptions> mongoOptions, ILogger<OrderRepository> logger)
    : RepositoryBase(serviceProvider, mongoOptions, logger), IOrderRepository
{
    /// <inheritdoc/>
    public async Task<OrderAggregate?> FindSucceededOrderByTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var collection = GetCollection<OrderAggregate>();

        var filter = Builders<OrderAggregate>.Filter.And(
            Builders<OrderAggregate>.Filter.Eq(x => x.TenantDetail.Id, tenantId),
            Builders<OrderAggregate>.Filter.Eq(x => x.PaymentStatus, PaymentStatus.Succeeded),
            Builders<OrderAggregate>.Filter.Eq(x => x.IsActive, true)
        );

        var sort = Builders<OrderAggregate>.Sort.Descending(x => x.CreatedAt);

        return await collection
            .Find(filter)
            .Sort(sort)
            .FirstOrDefaultAsync(cancellationToken);
    }
}
