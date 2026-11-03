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
            .Set(x => x.PeriodStart, order.PeriodStart)
            .Set(x => x.PeriodEnd, order.PeriodEnd)
            .Set(x => x.Packages, order.Packages)
            .Set(x => x.FailureReason, order.FailureReason)
            .Set(x => x.Error, order.Error)
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
    public async Task<bool> TouchPaymentPendingAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var collection = GetCollection<OrderAggregate>();

        var filter = Builders<OrderAggregate>.Filter.And(
            Builders<OrderAggregate>.Filter.Eq(x => x.Id, orderId),
            Builders<OrderAggregate>.Filter.Eq(x => x.ProvisioningStatus, ProvisioningStatus.PaymentPending));

        var update = Builders<OrderAggregate>.Update.Set(x => x.UpdatedAt, SystemClock.Instance.GetCurrentInstant());

        var result = await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        return result.ModifiedCount > 0;
    }

    /// <inheritdoc/>
    public async Task<List<OrderAggregate>> FindStuckOrdersAsync(ProvisioningStatus status, Duration stuckDuration, int limit, CancellationToken cancellationToken)
    {
        var collection = GetCollection<OrderAggregate>();
        var cutoff = SystemClock.Instance.GetCurrentInstant().Minus(stuckDuration);

        return await collection.Aggregate(StuckOrdersPipeline(status, cutoff, limit), cancellationToken: cancellationToken).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// El campo auxiliar con la fecha efectiva de la última actualización: solo vive dentro del pipeline.
    /// </summary>
    public const string EffectiveUpdatedAtField = "_EffectiveUpdatedAt";

    /// <summary>
    /// Las órdenes atascadas en un estado desde antes de <paramref name="cutoff"/>, de la más antigua a la más reciente.
    /// </summary>
    /// <remarks>
    /// Una orden que nunca se actualizó tiene <c>UpdatedAt</c> nulo, y Mongo no incluye un nulo en <c>$lt</c>: antes la
    /// conciliación nunca recogía una orden cuyo primer aviso de pago se perdió (orden <c>65b77cd4</c>, pendings/738). La
    /// fecha efectiva es <c>UpdatedAt</c> o, si no la tiene, <c>CreatedAt</c>; el orden también sale de ella, y el campo
    /// auxiliar se quita antes de leer el documento.
    /// </remarks>
    /// <param name="status">El estado de aprovisionamiento.</param>
    /// <param name="cutoff">Desde cuándo se considera atascada.</param>
    /// <param name="limit">Cuántas como mucho.</param>
    /// <returns>El pipeline.</returns>
    public static PipelineDefinition<OrderAggregate, OrderAggregate> StuckOrdersPipeline(ProvisioningStatus status, Instant cutoff, int limit)
    {
        var filter = Builders<OrderAggregate>.Filter;

        var match = filter.And(
            filter.Eq(x => x.ProvisioningStatus, status),
            filter.Eq(x => x.IsActive, true),
            filter.Or(
                filter.Lt(x => x.UpdatedAt, cutoff),
                filter.And(
                    filter.Eq(x => x.UpdatedAt, null),
                    filter.Lt(x => x.CreatedAt, cutoff))));

        return new EmptyPipelineDefinition<OrderAggregate>()
            .Match(match)
            .AppendStage<OrderAggregate, OrderAggregate, OrderAggregate>(new BsonDocument("$addFields", new BsonDocument(EffectiveUpdatedAtField, new BsonDocument("$ifNull", new BsonArray { "$UpdatedAt", "$CreatedAt" }))))
            .AppendStage<OrderAggregate, OrderAggregate, OrderAggregate>(new BsonDocument("$sort", new BsonDocument(EffectiveUpdatedAtField, 1)))
            .Limit(limit)
            .AppendStage<OrderAggregate, OrderAggregate, OrderAggregate>(new BsonDocument("$project", new BsonDocument(EffectiveUpdatedAtField, 0)));
    }

    /// <inheritdoc/>
    public async Task<Pagination<OrderAggregate>> GetAllOrdersAsync(CodeDesignPlus.Net.Core.Abstractions.Models.Criteria.Criteria criteria, CancellationToken cancellationToken)
    {
        var collection = GetCollection<OrderAggregate>();

        var filterExpression = criteria.GetFilterExpression<OrderAggregate>();
        var sortBy = criteria.GetSortByExpression<OrderAggregate>();

        // Sin filtrar por activas: el Super Admin ve también la orden que un cambio de licencia dejó inactiva y la que otra
        // reemplazó antes de pagarse (pendings/738). Quien quiera solo las activas lo pide en el criterio.
        var filter = Builders<OrderAggregate>.Filter.Where(filterExpression);

        var totalCount = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var query = collection.Find(filter);

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

    /// <inheritdoc />
    public async Task<bool> CreateIfAbsentAsync(OrderAggregate order, CancellationToken cancellationToken)
    {
        try
        {
            await CreateAsync(order, cancellationToken);

            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task RecordInitiatedPaymentAsync(Guid id, InitiatedPayment payment, CancellationToken cancellationToken)
    {
        var filter = Builders<OrderAggregate>.Filter.Eq(x => x.Id, id);
        var update = Builders<OrderAggregate>.Update.Set(x => x.InitiatedPayment, payment);

        return GetCollection<OrderAggregate>().UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    /// <inheritdoc />
    public Task<List<OrderAggregate>> FindPaidByTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var filter = Builders<OrderAggregate>.Filter.And(
            Builders<OrderAggregate>.Filter.Eq(x => x.TenantDetail.Id, tenantId),
            Builders<OrderAggregate>.Filter.Eq(x => x.PaymentStatus, PaymentStatus.Succeeded),
            Builders<OrderAggregate>.Filter.Eq(x => x.IsActive, true)
        );

        return GetCollection<OrderAggregate>().Find(filter).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<List<OrderAggregate>> FindPaidWithPackagesByTenantAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var filter = Builders<OrderAggregate>.Filter.And(
            Builders<OrderAggregate>.Filter.Eq(x => x.TenantDetail.Id, tenantId),
            WithPaidPackages());

        return GetCollection<OrderAggregate>().Find(filter).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<List<OrderAggregate>> FindAllPaidWithPackagesAsync(CancellationToken cancellationToken)
    {
        return GetCollection<OrderAggregate>().Find(WithPaidPackages()).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Pagadas, con al menos un paquete y sin reemplazar: lo pagado de una orden reemplazada fue a saldo a favor.
    /// </summary>
    private static FilterDefinition<OrderAggregate> WithPaidPackages() => Builders<OrderAggregate>.Filter.And(
        Builders<OrderAggregate>.Filter.Eq(x => x.PaymentStatus, PaymentStatus.Succeeded),
        Builders<OrderAggregate>.Filter.Eq(x => x.SupersededBy, null),
        Builders<OrderAggregate>.Filter.SizeGt(x => x.Packages, 0));

    /// <inheritdoc />
    public Task<List<OrderAggregate>> FindAwaitingPackagePaymentAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var filter = Builders<OrderAggregate>.Filter.And(
            Builders<OrderAggregate>.Filter.Eq(x => x.TenantDetail.Id, tenantId),
            Builders<OrderAggregate>.Filter.Eq(x => x.Type, OrderType.Package),
            Builders<OrderAggregate>.Filter.In(x => x.PaymentStatus, [PaymentStatus.Unknown, PaymentStatus.Initiated, PaymentStatus.Pending]),
            Builders<OrderAggregate>.Filter.Eq(x => x.IsActive, true)
        );

        return GetCollection<OrderAggregate>().Find(filter).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<List<OrderAggregate>> FindAllPaidAsync(CancellationToken cancellationToken)
    {
        var filter = Builders<OrderAggregate>.Filter.And(
            Builders<OrderAggregate>.Filter.Eq(x => x.PaymentStatus, PaymentStatus.Succeeded),
            Builders<OrderAggregate>.Filter.Eq(x => x.IsActive, true),
            Builders<OrderAggregate>.Filter.Ne(x => x.PeriodEnd, null)
        );

        return GetCollection<OrderAggregate>().Find(filter).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task<List<OrderAggregate>> FindAwaitingSubscriptionPaymentAsync(Guid tenantId, CancellationToken cancellationToken)
    {
        var filter = Builders<OrderAggregate>.Filter.And(
            Builders<OrderAggregate>.Filter.Eq(x => x.TenantDetail.Id, tenantId),
            Builders<OrderAggregate>.Filter.In(x => x.Type, [OrderType.Renewal, OrderType.Change]),
            Builders<OrderAggregate>.Filter.In(x => x.PaymentStatus, [PaymentStatus.Unknown, PaymentStatus.Initiated, PaymentStatus.Pending]),
            Builders<OrderAggregate>.Filter.Eq(x => x.IsActive, true)
        );

        return GetCollection<OrderAggregate>().Find(filter).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> SupersedeAsync(OrderAggregate order, CancellationToken cancellationToken)
    {
        // Solo si el pago sigue pendiente: si el webhook lo resolvió a la vez, gana el pago.
        var filter = Builders<OrderAggregate>.Filter.And(
            Builders<OrderAggregate>.Filter.Eq(x => x.Id, order.Id),
            Builders<OrderAggregate>.Filter.In(x => x.PaymentStatus, [PaymentStatus.Unknown, PaymentStatus.Initiated, PaymentStatus.Pending])
        );

        var update = Builders<OrderAggregate>.Update
            .Set(x => x.SupersededBy, order.SupersededBy)
            .Set(x => x.IsActive, false)
            .Set(x => x.UpdatedAt, order.UpdatedAt)
            .Set(x => x.UpdatedBy, order.UpdatedBy);

        var result = await GetCollection<OrderAggregate>().UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    /// <inheritdoc />
    public Task DeactivateAsync(IReadOnlyCollection<Guid> orderIds, Guid updatedBy, CancellationToken cancellationToken)
    {
        if (orderIds.Count == 0)
        {
            return Task.CompletedTask;
        }

        var filter = Builders<OrderAggregate>.Filter.In(x => x.Id, orderIds);

        var update = Builders<OrderAggregate>.Update
            .Set(x => x.IsActive, false)
            .Set(x => x.UpdatedAt, SystemClock.Instance.GetCurrentInstant())
            .Set(x => x.UpdatedBy, updatedBy);

        return GetCollection<OrderAggregate>().UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
    }
}
