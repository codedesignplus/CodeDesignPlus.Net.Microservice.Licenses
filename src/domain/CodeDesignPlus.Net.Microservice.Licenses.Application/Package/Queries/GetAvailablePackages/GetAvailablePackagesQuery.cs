using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Queries.GetAvailablePackages;

/// <summary>
/// Los paquetes que puede comprar o ampliar la copropiedad: los públicos más los privados que se le ofrecen (pendings/739).
/// </summary>
/// <param name="TenantId">La copropiedad del token.</param>
public record GetAvailablePackagesQuery(Guid TenantId) : IRequest<List<PackageDto>>;

/// <summary>
/// Lista los paquetes que se le ofrecen a la copropiedad.
/// </summary>
public class GetAvailablePackagesQueryHandler(IPackageRepository repository) : IRequestHandler<GetAvailablePackagesQuery, List<PackageDto>>
{
    /// <inheritdoc/>
    public async Task<List<PackageDto>> Handle(GetAvailablePackagesQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);
        ApplicationGuard.GuidIsEmpty(request.TenantId, Errors.TenantIdIsRequired);

        var packages = await repository.FindOnSaleAsync(cancellationToken);

        return [.. packages.Where(x => x.IsOfferedTo(request.TenantId)).Select(PackageDto.From).OrderBy(x => x.Price.Total).ThenBy(x => x.Name)];
    }
}
