namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;

public interface ILicenseRepository : IRepositoryBase
{
    Task<bool> LicesePopularityExistsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Guarda el registro nuevo si su id no existe todavía (pendings/589, regla 66).
    /// </summary>
    /// <param name="license">El agregado nuevo.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>false</c> si otro envío del mismo formulario ya lo guardó: el segundo choca contra la clave primaria.</returns>
    Task<bool> CreateIfAbsentAsync(LicenseAggregate license, CancellationToken cancellationToken);
}