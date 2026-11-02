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

    /// <summary>
    /// Las licencias del catálogo que siguen a la venta, para ofrecerlas como cambio de licencia (pendings/738).
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Las licencias activas y no borradas.</returns>
    Task<List<LicenseAggregate>> FindOnSaleAsync(CancellationToken cancellationToken);
}
