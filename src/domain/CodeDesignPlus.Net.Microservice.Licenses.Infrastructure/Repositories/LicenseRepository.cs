namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Repositories;

public class LicenseRepository(IServiceProvider serviceProvider, IOptions<MongoOptions> mongoOptions, ILogger<LicenseRepository> logger) 
    : RepositoryBase(serviceProvider, mongoOptions, logger), ILicenseRepository
{
   
}