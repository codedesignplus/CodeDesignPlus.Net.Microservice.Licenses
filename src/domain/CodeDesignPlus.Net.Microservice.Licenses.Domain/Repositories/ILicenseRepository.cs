namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;

public interface ILicenseRepository : IRepositoryBase
{
    Task<bool> LicesePopularityExistsAsync(Guid id, CancellationToken cancellationToken);
}