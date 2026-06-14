using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

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

    /// <inheritdoc/>
    public async Task<OrderAggregate?> FindActiveSubscriptionAsync(Guid tenantId, Guid licenseId, CancellationToken cancellationToken)
    {
        var collection = GetCollection<OrderAggregate>();

        var filter = Builders<OrderAggregate>.Filter.And(
            Builders<OrderAggregate>.Filter.Eq(x => x.TenantDetail.Id, tenantId),
            Builders<OrderAggregate>.Filter.Eq(x => x.License.Id, licenseId),
            Builders<OrderAggregate>.Filter.Eq(x => x.PaymentStatus, PaymentStatus.Succeeded),
            Builders<OrderAggregate>.Filter.Eq(x => x.IsActive, true)
        );

        var sort = Builders<OrderAggregate>.Sort.Descending(x => x.CreatedAt);

        return await collection
            .Find(filter)
            .Sort(sort)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<OrderAggregate?> CompleteProvisioningStepAtomicAsync(Guid orderId, string stepName, CancellationToken cancellationToken)
    {
        var collection = GetCollection<OrderAggregate>();
        var now = SystemClock.Instance.GetCurrentInstant();

        var filter = Builders<OrderAggregate>.Filter.And(
            Builders<OrderAggregate>.Filter.Eq(x => x.Id, orderId),
            Builders<OrderAggregate>.Filter.Not(
                Builders<OrderAggregate>.Filter.ElemMatch(
                    x => x.ProvisioningHistory,
                    Builders<ProvisioningStep>.Filter.And(
                        Builders<ProvisioningStep>.Filter.Eq(s => s.StepName, stepName),
                        Builders<ProvisioningStep>.Filter.Eq(s => s.Status, ProvisioningStepStatus.Completed)
                    )
                )
            )
        );

        var update = Builders<OrderAggregate>.Update
            .Push(x => x.ProvisioningHistory, new ProvisioningStep(stepName, ProvisioningStepStatus.Completed, now))
            .Set(x => x.UpdatedAt, now);

        var options = new FindOneAndUpdateOptions<OrderAggregate>
        {
            ReturnDocument = ReturnDocument.After
        };

        return await collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
    }
}
