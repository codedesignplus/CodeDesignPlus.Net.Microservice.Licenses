using CodeDesignPlus.Net.Core.Abstractions.Models.Criteria;
using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.Criteria.Extensions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using C = CodeDesignPlus.Net.Core.Abstractions.Models;

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

    /// <inheritdoc/>
    public async Task<bool> ApplyPaymentStatusAsync(OrderAggregate order, PaymentStatus previousStatus, CancellationToken cancellationToken)
    {
        var collection = GetCollection<OrderAggregate>();

        var filter = Builders<OrderAggregate>.Filter.And(
            Builders<OrderAggregate>.Filter.Eq(x => x.Id, order.Id),
            Builders<OrderAggregate>.Filter.Eq(x => x.PaymentStatus, previousStatus)
        );

        // El historial se escribe entero porque aqui nace: los pasos pendientes se crean al aplicar el pago y
        // nadie mas escribe en el hasta que se publica el evento, que va despues de esta llamada.
        var update = Builders<OrderAggregate>.Update
            .Set(x => x.PaymentStatus, order.PaymentStatus)
            .Set(x => x.IsSuccess, order.IsSuccess)
            .Set(x => x.ProvisioningStatus, order.ProvisioningStatus)
            .Set(x => x.ProvisioningHistory, order.ProvisioningHistory)
            .Set(x => x.UpdatedAt, order.UpdatedAt);

        var result = await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    /// <inheritdoc/>
    public async Task AttachReceiptAsync(Guid orderId, FileAttachment receipt, Guid updatedBy, CancellationToken cancellationToken)
    {
        var collection = GetCollection<OrderAggregate>();

        var update = Builders<OrderAggregate>.Update
            .Set(x => x.Receipt, receipt)
            .Set(x => x.UpdatedAt, SystemClock.Instance.GetCurrentInstant())
            .Set(x => x.UpdatedBy, updatedBy);

        await collection.UpdateOneAsync(Builders<OrderAggregate>.Filter.Eq(x => x.Id, orderId), update, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> MarkProvisioningCompletedAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var collection = GetCollection<OrderAggregate>();

        var filter = Builders<OrderAggregate>.Filter.And(
            Builders<OrderAggregate>.Filter.Eq(x => x.Id, orderId),
            Builders<OrderAggregate>.Filter.Eq(x => x.ProvisioningStatus, ProvisioningStatus.InProgress)
        );

        var update = Builders<OrderAggregate>.Update
            .Set(x => x.ProvisioningStatus, ProvisioningStatus.Completed)
            .Set(x => x.UpdatedAt, SystemClock.Instance.GetCurrentInstant());

        var result = await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    /// <inheritdoc/>
    public async Task<bool> FailProvisioningAsync(Guid orderId, ProvisioningStep failedStep, Guid updatedBy, CancellationToken cancellationToken)
    {
        var collection = GetCollection<OrderAggregate>();

        var filter = Builders<OrderAggregate>.Filter.And(
            Builders<OrderAggregate>.Filter.Eq(x => x.Id, orderId),
            Builders<OrderAggregate>.Filter.Eq(x => x.ProvisioningStatus, ProvisioningStatus.InProgress)
        );

        var update = Builders<OrderAggregate>.Update
            .Push(x => x.ProvisioningHistory, failedStep)
            .Set(x => x.ProvisioningStatus, ProvisioningStatus.PartiallyFailed)
            .Set(x => x.UpdatedAt, SystemClock.Instance.GetCurrentInstant())
            .Set(x => x.UpdatedBy, updatedBy);

        var result = await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    /// <inheritdoc/>
    public async Task<int?> RegisterProvisioningAttemptAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var collection = GetCollection<OrderAggregate>();

        var filter = Builders<OrderAggregate>.Filter.And(
            Builders<OrderAggregate>.Filter.Eq(x => x.Id, orderId),
            Builders<OrderAggregate>.Filter.Eq(x => x.ProvisioningStatus, ProvisioningStatus.InProgress)
        );

        var update = Builders<OrderAggregate>.Update
            .Inc(x => x.ProvisioningAttempts, 1)
            .Set(x => x.UpdatedAt, SystemClock.Instance.GetCurrentInstant());

        var options = new FindOneAndUpdateOptions<OrderAggregate> { ReturnDocument = ReturnDocument.After };

        var order = await collection.FindOneAndUpdateAsync(filter, update, options, cancellationToken);

        return order?.ProvisioningAttempts;
    }

    /// <inheritdoc/>
    public async Task<List<OrderAggregate>> FindStuckOrdersAsync(ProvisioningStatus status, Duration stuckDuration, int limit, CancellationToken cancellationToken)
    {
        var collection = GetCollection<OrderAggregate>();
        var cutoff = SystemClock.Instance.GetCurrentInstant().Minus(stuckDuration);

        var filter = Builders<OrderAggregate>.Filter.And(
            Builders<OrderAggregate>.Filter.Eq(x => x.ProvisioningStatus, status),
            Builders<OrderAggregate>.Filter.Eq(x => x.IsActive, true),
            Builders<OrderAggregate>.Filter.Lt(x => x.UpdatedAt, cutoff)
        );

        var sort = Builders<OrderAggregate>.Sort.Ascending(x => x.UpdatedAt);

        return await collection
            .Find(filter)
            .Sort(sort)
            .Limit(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<Pagination<OrderAggregate>> GetAllOrdersAsync(CodeDesignPlus.Net.Core.Abstractions.Models.Criteria.Criteria criteria, CancellationToken cancellationToken)
    {
        var collection = GetCollection<OrderAggregate>();

        var filterExpression = criteria.GetFilterExpression<OrderAggregate>();
        var sortBy = criteria.GetSortByExpression<OrderAggregate>();

        var filter = Builders<OrderAggregate>.Filter.And(
            Builders<OrderAggregate>.Filter.Where(filterExpression),
            Builders<OrderAggregate>.Filter.Eq(x => x.IsActive, true)
        );

        var totalCount = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        // Orden natural: las cifras de un texto se comparan como numeros ("T1-101" antes que "T1-1001"). pendings/150.
        var query = collection.Find(filter, new FindOptions { Collation = new Collation("es", numericOrdering: true) });

        if (sortBy != null)
        {
            query = criteria.OrderType == OrderTypes.Ascending
                ? query.SortBy(sortBy)
                : query.SortByDescending(sortBy);
        }

        if (criteria.Skip.HasValue)
            query = query.Skip(criteria.Skip.Value);

        if (criteria.Limit.HasValue)
            query = query.Limit(criteria.Limit.Value);

        var data = await query.ToListAsync(cancellationToken);

        return Pagination<OrderAggregate>.Create(data, totalCount, limit: criteria.Limit, skip: criteria.Skip);
    }
}
