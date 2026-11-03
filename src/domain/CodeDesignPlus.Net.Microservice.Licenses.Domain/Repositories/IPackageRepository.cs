using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;

/// <summary>
/// El catálogo de paquetes complementarios (pendings/739).
/// </summary>
public interface IPackageRepository : IRepositoryBase
{
    /// <summary>
    /// Guarda el paquete nuevo si su id no existe todavía (regla 66).
    /// </summary>
    /// <param name="package">El paquete.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns><c>false</c> si otro envío del mismo formulario ya lo guardó.</returns>
    Task<bool> CreateIfAbsentAsync(PackageAggregate package, CancellationToken cancellationToken);

    /// <summary>
    /// Los paquetes activos y no borrados: de ellos salen los que se le ofrecen a una copropiedad.
    /// </summary>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Los paquetes.</returns>
    Task<List<PackageAggregate>> FindOnSaleAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Los paquetes del catálogo público que cumplen el criterio: solo los visibles en la landing y no borrados. El criterio
    /// se suma a esa base y no la puede ampliar.
    /// </summary>
    /// <param name="criteria">El criterio de la pantalla.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La página.</returns>
    Task<Pagination<PackageAggregate>> MatchingShownInLandingPageAsync(CodeDesignPlus.Net.Core.Abstractions.Models.Criteria.Criteria criteria, CancellationToken cancellationToken);
}
