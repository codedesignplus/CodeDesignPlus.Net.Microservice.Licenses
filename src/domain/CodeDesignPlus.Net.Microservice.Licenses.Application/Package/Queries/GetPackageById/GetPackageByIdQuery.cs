using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Queries.GetPackageById;

/// <summary>
/// Un paquete por su id (pendings/739).
/// </summary>
/// <param name="Id">El paquete.</param>
/// <param name="ForSaleOnly"><c>true</c> en el catálogo público: uno privado o inactivo responde como si no existiera.</param>
public record GetPackageByIdQuery(Guid Id, bool ForSaleOnly) : IRequest<PackageDto>;

/// <summary>
/// Lee un paquete.
/// </summary>
public class GetPackageByIdQueryHandler(IPackageRepository repository) : IRequestHandler<GetPackageByIdQuery, PackageDto>
{
    /// <inheritdoc/>
    public async Task<PackageDto> Handle(GetPackageByIdQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var package = await repository.FindAsync<PackageAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsTrue(package is null || (request.ForSaleOnly && !package.IsForSale), Errors.PackageNotFound);

        return PackageDto.From(package!);
    }
}
