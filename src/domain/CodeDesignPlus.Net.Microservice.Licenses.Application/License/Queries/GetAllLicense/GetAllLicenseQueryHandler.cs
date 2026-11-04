using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetAllLicense;

public class GetAllLicenseQueryHandler(ILicenseRepository repository, IOrderRepository orders, IMapper mapper) : IRequestHandler<GetAllLicenseQuery, Pagination<LicenseDto>>
{
    public async Task<Pagination<LicenseDto>> Handle(GetAllLicenseQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var licenses = request.ForSaleOnly
            ? await repository.MatchingShownInLandingPageAsync(request.Criteria, cancellationToken)
            : await repository.MatchingAsync<LicenseAggregate>(request.Criteria, cancellationToken);

        var licensesDto = new List<LicenseDto>();

        foreach (var license in licenses.Data)
        {
            var licenseDto = mapper.Map<LicenseDto>(license);

            foreach (var price in license.Prices)
            {
                licenseDto.Prices.Add(PriceDto.FromDomain(price));
            }

            // The platform view says which offers can no longer be withdrawn (rule 42, pendings/464).
            if (!request.ForSaleOnly)
            {
                await LicenseOffers.FillOfferStatusAsync(licenseDto, orders, cancellationToken);
            }

            licensesDto.Add(licenseDto);
        }

        return Pagination<LicenseDto>.Create(licensesDto, licenses.TotalCount, licenses.Limit, licenses.Skip);
    }
}
