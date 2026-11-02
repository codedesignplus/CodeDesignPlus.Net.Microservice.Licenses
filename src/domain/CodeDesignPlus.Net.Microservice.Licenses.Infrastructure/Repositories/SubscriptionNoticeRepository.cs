namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Repositories;

/// <summary>
/// El repositorio de las constancias de los avisos de vencimiento (pendings/736).
/// </summary>
public class SubscriptionNoticeRepository(IServiceProvider serviceProvider, IOptions<MongoOptions> mongoOptions, ILogger<SubscriptionNoticeRepository> logger)
    : RepositoryBase(serviceProvider, mongoOptions, logger), ISubscriptionNoticeRepository
{
    /// <inheritdoc />
    public async Task<bool> CreateIfAbsentAsync(SubscriptionNoticeAggregate notice, CancellationToken cancellationToken)
    {
        try
        {
            await CreateAsync(notice, cancellationToken);

            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task<List<SubscriptionNoticeAggregate>> FindUnpublishedAsync(CancellationToken cancellationToken)
    {
        return GetCollection<SubscriptionNoticeAggregate>().Find(x => x.Published == false).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public Task MarkPublishedAsync(Guid id, Instant publishedAt, CancellationToken cancellationToken)
    {
        var update = Builders<SubscriptionNoticeAggregate>.Update
            .Set(x => x.Published, true)
            .Set(x => x.UpdatedAt, publishedAt);

        return GetCollection<SubscriptionNoticeAggregate>().UpdateOneAsync(x => x.Id == id, update, cancellationToken: cancellationToken);
    }
}
