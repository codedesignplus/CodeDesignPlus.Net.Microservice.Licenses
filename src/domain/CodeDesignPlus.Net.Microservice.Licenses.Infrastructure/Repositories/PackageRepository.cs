using CodeDesignPlus.Net.Core.Abstractions.Models.Criteria;
using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.Criteria.Extensions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Repositories;

/// <summary>
/// El catálogo de paquetes en MongoDB (pendings/739).
/// </summary>
public class PackageRepository(IServiceProvider serviceProvider, IOptions<MongoOptions> mongoOptions, ILogger<PackageRepository> logger)
    : RepositoryBase(serviceProvider, mongoOptions, logger), IPackageRepository
{
    /// <inheritdoc />
    public async Task<bool> CreateIfAbsentAsync(PackageAggregate package, CancellationToken cancellationToken)
    {
        try
        {
            await CreateAsync(package, cancellationToken);

            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task<List<PackageAggregate>> FindOnSaleAsync(CancellationToken cancellationToken)
    {
        return GetCollection<PackageAggregate>().Find(x => x.IsActive == true && x.IsDeleted == false).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Pagination<PackageAggregate>> MatchingShownInLandingPageAsync(CodeDesignPlus.Net.Core.Abstractions.Models.Criteria.Criteria criteria, CancellationToken cancellationToken)
    {
        var collection = GetCollection<PackageAggregate>();
        var builder = Builders<PackageAggregate>.Filter;

        var filter = builder.And(
            builder.Where(criteria.GetFilterExpression<PackageAggregate>()),
            builder.Eq(x => x.ShowInLandingPage, true),
            builder.Eq(x => x.IsActive, true),
            builder.Eq(x => x.IsDeleted, false));

        var totalCount = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var query = collection.Find(filter);
        var sortBy = criteria.GetSortByExpression<PackageAggregate>();

        if (sortBy != null)
        {
            query = criteria.OrderType == OrderTypes.Ascending ? query.SortBy(sortBy) : query.SortByDescending(sortBy);
        }

        if (criteria.Skip.HasValue)
        {
            query = query.Skip(criteria.Skip.Value);
        }

        if (criteria.Limit.HasValue)
        {
            query = query.Limit(criteria.Limit.Value);
        }

        var data = await query.ToListAsync(cancellationToken);

        return Pagination<PackageAggregate>.Create(data, totalCount, limit: criteria.Limit, skip: criteria.Skip);
    }
}
