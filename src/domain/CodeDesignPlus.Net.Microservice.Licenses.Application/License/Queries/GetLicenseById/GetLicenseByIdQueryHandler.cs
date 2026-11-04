using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetLicenseById;

public class GetLicenseByIdQueryHandler(ILicenseRepository repository, IOrderRepository orders, IMapper mapper, ICacheManager cacheManager) : IRequestHandler<GetLicenseByIdQuery, LicenseDto>
{
    public async Task<LicenseDto> Handle(GetLicenseByIdQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var exists = await cacheManager.ExistsAsync(request.Id.ToString());

        if (exists)
            return await VisibleAsync(request, await cacheManager.GetAsync<LicenseDto>(request.Id.ToString()), cancellationToken);

        var license = await repository.FindAsync<LicenseAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsNull(license, Errors.LicenseNotFound);

        var dto = mapper.Map<LicenseDto>(license);

        foreach (var price in license.Prices)
        {
            dto.Prices.Add(PriceDto.FromDomain(price));
        }

        await cacheManager.SetAsync(request.Id.ToString(), dto);

        return await VisibleAsync(request, dto, cancellationToken);
    }

    /// <summary>
    /// El catálogo público no ve una licencia privada: responde igual que si no existiera (pendings/464). La vista de
    /// plataforma suma, en cada oferta, si la copropiedad ya la tomó o la está pagando; se calcula después de la caché,
    /// porque depende de las órdenes y no de la licencia (regla 42).
    /// </summary>
    private async Task<LicenseDto> VisibleAsync(GetLicenseByIdQuery request, LicenseDto dto, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsTrue(request.ForSaleOnly && !dto.ShowInLandingPage, Errors.LicenseNotFound);

        if (!request.ForSaleOnly)
        {
            await LicenseOffers.FillOfferStatusAsync(dto, orders, cancellationToken);
        }

        return dto;
    }
}
