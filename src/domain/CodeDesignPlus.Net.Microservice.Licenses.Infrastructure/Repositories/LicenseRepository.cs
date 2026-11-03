using CodeDesignPlus.Net.Core.Abstractions.Models.Criteria;
using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.Criteria.Extensions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Repositories;

public class LicenseRepository(IServiceProvider serviceProvider, IOptions<MongoOptions> mongoOptions, ILogger<LicenseRepository> logger)
    : RepositoryBase(serviceProvider, mongoOptions, logger), ILicenseRepository
{
    public Task<bool> LicesePopularityExistsAsync(Guid id, CancellationToken cancellationToken)
    {
        var collection = this.GetCollection<LicenseAggregate>();

        return collection.Find(x => x.Id != id && x.IsPopular == true && x.IsActive == true).AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> CreateIfAbsentAsync(LicenseAggregate license, CancellationToken cancellationToken)
    {
        try
        {
            await CreateAsync(license, cancellationToken);

            return true;
        }
        catch (MongoWriteException exception) when (exception.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    /// <inheritdoc />
    public Task<List<LicenseAggregate>> FindOnSaleAsync(CancellationToken cancellationToken)
    {
        return GetCollection<LicenseAggregate>().Find(x => x.IsActive == true && x.IsDeleted == false).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Pagination<LicenseAggregate>> MatchingShownInLandingPageAsync(CodeDesignPlus.Net.Core.Abstractions.Models.Criteria.Criteria criteria, CancellationToken cancellationToken)
    {
        var collection = GetCollection<LicenseAggregate>();
        var builder = Builders<LicenseAggregate>.Filter;

        var filter = builder.And(
            builder.Where(criteria.GetFilterExpression<LicenseAggregate>()),
            builder.Eq(x => x.ShowInLandingPage, true),
            builder.Eq(x => x.IsDeleted, false));

        var totalCount = await collection.CountDocumentsAsync(filter, cancellationToken: cancellationToken);

        var query = collection.Find(filter);
        var sortBy = criteria.GetSortByExpression<LicenseAggregate>();

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

        return Pagination<LicenseAggregate>.Create(data, totalCount, limit: criteria.Limit, skip: criteria.Skip);
    }
}
