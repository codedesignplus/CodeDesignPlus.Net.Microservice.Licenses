namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Repositories;

/// <summary>
/// El repositorio del saldo a favor de las copropiedades (pendings/738).
/// </summary>
public class CreditBalanceRepository(IServiceProvider serviceProvider, IOptions<MongoOptions> mongoOptions, ILogger<CreditBalanceRepository> logger)
    : RepositoryBase(serviceProvider, mongoOptions, logger), ICreditBalanceRepository
{
}
