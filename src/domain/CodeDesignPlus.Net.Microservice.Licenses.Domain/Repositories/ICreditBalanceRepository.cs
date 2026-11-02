namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;

/// <summary>
/// El repositorio del saldo a favor de las copropiedades (pendings/738). Se escribe con
/// <c>UpdateWithRetryAsync</c>, porque el saldo es un documento versionado.
/// </summary>
public interface ICreditBalanceRepository : IRepositoryBase
{
}
