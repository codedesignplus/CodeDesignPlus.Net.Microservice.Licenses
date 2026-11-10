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

        var update = PaymentStatusUpdate(order);

        var result = await collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    /// <summary>
    /// What the result of the payment writes on the order: only its own fields, never the whole document.
    /// </summary>
    /// <param name="order">The order with the payment applied.</param>
    /// <returns>The update.</returns>
    public static UpdateDefinition<OrderAggregate> PaymentStatusUpdate(OrderAggregate order)
    {
        // El historial se escribe entero porque aqui nace: los pasos pendientes se crean al aplicar el pago y
        // nadie mas escribe en el hasta que se publica el evento, que va despues de esta llamada.
        return Builders<OrderAggregate>.Update
            .Set(x => x.PaymentStatus, order.PaymentStatus)
            .Set(x => x.IsSuccess, order.IsSuccess)
            .Set(x => x.ProvisioningStatus, order.ProvisioningStatus)
            .Set(x => x.ProvisioningHistory, order.ProvisioningHistory)
            .Set(x => x.PeriodStart, order.PeriodStart)
            .Set(x => x.PeriodEnd, order.PeriodEnd)
            .Set(x => x.Packages, order.Packages)
            .Set(x => x.FailureReason, order.FailureReason)
            .Set(x => x.Error, order.Error)
            .Set(x => x.PaymentMethodType, order.PaymentMethodType)
            .Set(x => x.CardType, order.CardType)
            .Set(x => x.UpdatedAt, order.UpdatedAt);
    }

    /// <inheritdoc/>
    public async Task<bool> RequestElectronicDocumentsAsync(OrderAggregate order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        var update = Builders<OrderAggregate>.Update
            .Set(x => x.Invoice, order.Invoice)
            .Set(x => x.CreditNotes, order.CreditNotes);

        var result = await GetCollection<OrderAggregate>().UpdateOneAsync(RequestElectronicDocumentsFilter(order.Id), update, cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    /// <summary>
    /// Which order takes the requested documents: the one with that id that has no invoice yet. A redelivery cannot turn a
    /// validated invoice back into a requested one (pendings/661).
    /// </summary>
    /// <param name="orderId">The order.</param>
    /// <returns>The filter.</returns>
    public static FilterDefinition<OrderAggregate> RequestElectronicDocumentsFilter(Guid orderId) => Builders<OrderAggregate>.Filter.And(
        Builders<OrderAggregate>.Filter.Eq(x => x.Id, orderId),
        Builders<OrderAggregate>.Filter.Eq(x => x.Invoice, null));

    /// <inheritdoc/>
    public async Task<bool> RecordElectronicDocumentAsync(Guid orderId, OrderElectronicDocument document, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        var (filter, update) = RecordElectronicDocumentWrite(orderId, document);
        var result = await GetCollection<OrderAggregate>().UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        // Lo que el filtro encontró, no lo que cambió: la reentrega de un rechazo vuelve a avisar con el mismo id.
        return result.MatchedCount == 1;
    }

    /// <summary>
    /// How the answer of ms-electronicinvoicing is written on the order (pendings/661): the invoice, or the credit note in
    /// its place of the array, only while that document is of the order and is not validated. A late rejection does not
    /// undo a validation, and the other notes of the order may be changing at the same time.
    /// </summary>
    /// <param name="orderId">The order.</param>
    /// <param name="document">The document as it stands now.</param>
    /// <returns>The filter and the update.</returns>
    public static (FilterDefinition<OrderAggregate> Filter, UpdateDefinition<OrderAggregate> Update) RecordElectronicDocumentWrite(Guid orderId, OrderElectronicDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = Builders<OrderAggregate>.Filter;

        if (document.CorrectedOrderId is null)
        {
            var invoiceFilter = builder.And(
                builder.Eq(x => x.Id, orderId),
                builder.Eq(x => x.Invoice!.DocumentId, document.DocumentId),
                builder.Ne(x => x.Invoice!.State, ElectronicDocumentState.Validated));

            return (invoiceFilter, Builders<OrderAggregate>.Update.Set(x => x.Invoice, document));
        }

        var noteFilter = builder.And(
            builder.Eq(x => x.Id, orderId),
            builder.ElemMatch(x => x.CreditNotes, Builders<OrderElectronicDocument>.Filter.And(
                Builders<OrderElectronicDocument>.Filter.Eq(x => x.DocumentId, document.DocumentId),
                Builders<OrderElectronicDocument>.Filter.Ne(x => x.State, ElectronicDocumentState.Validated))));

        return (noteFilter, Builders<OrderAggregate>.Update.Set($"{nameof(OrderAggregate.CreditNotes)}.$", document));
    }

    /// <inheritdoc/>
    public async Task AttachReceiptAsync(Guid orderId, FileAttachment receipt, Instant? emailWaitsUntil, Guid updatedBy, CancellationToken cancellationToken)
    {
        var collection = GetCollection<OrderAggregate>();

        var update = Builders<OrderAggregate>.Update
            .Set(x => x.Receipt, receipt)
            .Set(x => x.ReceiptEmailWaitsUntil, emailWaitsUntil)
            .Set(x => x.UpdatedAt, SystemClock.Instance.GetCurrentInstant())
            .Set(x => x.UpdatedBy, updatedBy);

        await collection.UpdateOneAsync(Builders<OrderAggregate>.Filter.Eq(x => x.Id, orderId), update, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> ClaimReceiptEmailAsync(Guid orderId, bool withInvoice, Instant sentAt, CancellationToken cancellationToken)
    {
        var (filter, update) = ClaimReceiptEmailWrite(orderId, withInvoice, sentAt);
        var result = await GetCollection<OrderAggregate>().UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    /// <inheritdoc/>
    public async Task<bool> ClaimInvoiceEmailAsync(Guid orderId, Instant sentAt, CancellationToken cancellationToken)
    {
        var (filter, update) = ClaimInvoiceEmailWrite(orderId, sentAt);
        var result = await GetCollection<OrderAggregate>().UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        return result.ModifiedCount == 1;
    }

    /// <inheritdoc/>
    public async Task ReleaseReceiptEmailAsync(Guid orderId, bool withInvoice, Instant sentAt, CancellationToken cancellationToken)
    {
        var (filter, update) = ReleaseReceiptEmailWrite(orderId, withInvoice, sentAt);

        await GetCollection<OrderAggregate>().UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task ReleaseInvoiceEmailAsync(Guid orderId, Instant sentAt, CancellationToken cancellationToken)
    {
        var filter = Builders<OrderAggregate>.Filter.And(Builders<OrderAggregate>.Filter.Eq(x => x.Id, orderId), Builders<OrderAggregate>.Filter.Eq(x => x.InvoiceEmailSentAt, sentAt));
        var update = Builders<OrderAggregate>.Update.Set(x => x.InvoiceEmailSentAt, null);

        await GetCollection<OrderAggregate>().UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<List<OrderAggregate>> FindReceiptEmailsOverdueAsync(Instant now, int limit, CancellationToken cancellationToken)
    {
        var sort = Builders<OrderAggregate>.Sort.Ascending(x => x.ReceiptEmailWaitsUntil);

        return await GetCollection<OrderAggregate>().Find(ReceiptEmailsOverdueFilter(now)).Sort(sort).Limit(limit).ToListAsync(cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<bool> RecordElectronicDocumentFilesAsync(Guid orderId, OrderElectronicDocument document, CancellationToken cancellationToken)
    {
        var (filter, update) = RecordElectronicDocumentFilesWrite(orderId, document);
        var result = await GetCollection<OrderAggregate>().UpdateOneAsync(filter, update, cancellationToken: cancellationToken);

        return result.MatchedCount == 1;
    }

    /// <summary>
    /// How the email of the receipt is reserved (pendings/661, step 4): only while it has not been sent and, when it carries
    /// the invoice, while the invoice has not been sent either. What the filter matched is what sends it.
    /// </summary>
    /// <param name="orderId">The order.</param>
    /// <param name="withInvoice">Whether the email carries the invoice.</param>
    /// <param name="sentAt">When it is sent.</param>
    /// <returns>The filter and the update.</returns>
    public static (FilterDefinition<OrderAggregate> Filter, UpdateDefinition<OrderAggregate> Update) ClaimReceiptEmailWrite(Guid orderId, bool withInvoice, Instant sentAt)
    {
        var builder = Builders<OrderAggregate>.Filter;
        var filter = builder.And(builder.Eq(x => x.Id, orderId), builder.Eq(x => x.ReceiptEmailSentAt, null));
        var update = Builders<OrderAggregate>.Update.Set(x => x.ReceiptEmailSentAt, sentAt);

        if (withInvoice)
        {
            filter = builder.And(filter, builder.Eq(x => x.InvoiceEmailSentAt, null));
            update = update.Set(x => x.InvoiceEmailSentAt, sentAt);
        }

        return (filter, update);
    }

    /// <summary>
    /// How the separate email of the invoice is reserved: only after the email of the receipt and while the invoice has not
    /// been sent.
    /// </summary>
    /// <param name="orderId">The order.</param>
    /// <param name="sentAt">When it is sent.</param>
    /// <returns>The filter and the update.</returns>
    public static (FilterDefinition<OrderAggregate> Filter, UpdateDefinition<OrderAggregate> Update) ClaimInvoiceEmailWrite(Guid orderId, Instant sentAt)
    {
        var builder = Builders<OrderAggregate>.Filter;
        var filter = builder.And(builder.Eq(x => x.Id, orderId), builder.Ne(x => x.ReceiptEmailSentAt, null), builder.Eq(x => x.InvoiceEmailSentAt, null));

        return (filter, Builders<OrderAggregate>.Update.Set(x => x.InvoiceEmailSentAt, sentAt));
    }

    /// <summary>
    /// How a reservation that could not be published is given back: only the one this process made, by its instant.
    /// </summary>
    /// <param name="orderId">The order.</param>
    /// <param name="withInvoice">Whether the email carried the invoice.</param>
    /// <param name="sentAt">The instant of the reservation.</param>
    /// <returns>The filter and the update.</returns>
    public static (FilterDefinition<OrderAggregate> Filter, UpdateDefinition<OrderAggregate> Update) ReleaseReceiptEmailWrite(Guid orderId, bool withInvoice, Instant sentAt)
    {
        var builder = Builders<OrderAggregate>.Filter;
        var filter = builder.And(builder.Eq(x => x.Id, orderId), builder.Eq(x => x.ReceiptEmailSentAt, sentAt));
        var update = Builders<OrderAggregate>.Update.Set(x => x.ReceiptEmailSentAt, null);

        if (withInvoice)
        {
            filter = builder.And(filter, builder.Eq(x => x.InvoiceEmailSentAt, sentAt));
            update = update.Set(x => x.InvoiceEmailSentAt, null);
        }

        return (filter, update);
    }

    /// <summary>
    /// The orders whose email waited for the invoice past its time and has not been sent.
    /// </summary>
    /// <param name="now">The current instant.</param>
    /// <returns>The filter.</returns>
    public static FilterDefinition<OrderAggregate> ReceiptEmailsOverdueFilter(Instant now)
    {
        var builder = Builders<OrderAggregate>.Filter;

        return builder.And(builder.Ne(x => x.ReceiptEmailWaitsUntil, null), builder.Lte(x => x.ReceiptEmailWaitsUntil, now), builder.Eq(x => x.ReceiptEmailSentAt, null));
    }

    /// <summary>
    /// How the files kept after the validation are written: the invoice, or the credit note in its place of the array, only
    /// while that document is of the order and is validated.
    /// </summary>
    /// <param name="orderId">The order.</param>
    /// <param name="document">The document with its files.</param>
    /// <returns>The filter and the update.</returns>
    public static (FilterDefinition<OrderAggregate> Filter, UpdateDefinition<OrderAggregate> Update) RecordElectronicDocumentFilesWrite(Guid orderId, OrderElectronicDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        var builder = Builders<OrderAggregate>.Filter;

        if (document.CorrectedOrderId is null)
        {
            var invoiceFilter = builder.And(
                builder.Eq(x => x.Id, orderId),
                builder.Eq(x => x.Invoice!.DocumentId, document.DocumentId),
                builder.Eq(x => x.Invoice!.State, ElectronicDocumentState.Validated));

            return (invoiceFilter, Builders<OrderAggregate>.Update.Set(x => x.Invoice, document));
        }

        var noteFilter = builder.And(
            builder.Eq(x => x.Id, orderId),
            builder.ElemMatch(x => x.CreditNotes, Builders<OrderElectronicDocument>.Filter.And(
                Builders<OrderElectronicDocument>.Filter.Eq(x => x.DocumentId, document.DocumentId),
                Builders<OrderElectronicDocument>.Filter.Eq(x => x.State, ElectronicDocumentState.Validated))));

        return (noteFilter, Builders<OrderAggregate>.Update.Set($"{nameof(OrderAggregate.CreditNotes)}.$", document));
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
