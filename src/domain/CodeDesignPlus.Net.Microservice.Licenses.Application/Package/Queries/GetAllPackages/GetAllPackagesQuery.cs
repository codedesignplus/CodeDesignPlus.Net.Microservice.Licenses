using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Queries.GetAllPackages;

/// <summary>
/// Lista los paquetes que cumplen el criterio (pendings/739).
/// </summary>
/// <param name="Criteria">El criterio de la pantalla.</param>
/// <param name="ForSaleOnly"><c>true</c> en el catálogo público: solo los visibles en la landing.</param>
public record GetAllPackagesQuery(C.Criteria Criteria, bool ForSaleOnly) : IRequest<Pagination<PackageDto>>;

/// <summary>
/// Lista los paquetes.
/// </summary>
public class GetAllPackagesQueryHandler(IPackageRepository repository) : IRequestHandler<GetAllPackagesQuery, Pagination<PackageDto>>
{
    /// <inheritdoc/>
    public async Task<Pagination<PackageDto>> Handle(GetAllPackagesQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var page = request.ForSaleOnly
            ? await repository.MatchingShownInLandingPageAsync(request.Criteria, cancellationToken)
            : await repository.MatchingAsync<PackageAggregate>(request.Criteria, cancellationToken);

        return Pagination<PackageDto>.Create([.. page.Data.Select(PackageDto.From)], page.TotalCount, page.Limit, page.Skip);
    }
}
