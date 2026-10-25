
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
}