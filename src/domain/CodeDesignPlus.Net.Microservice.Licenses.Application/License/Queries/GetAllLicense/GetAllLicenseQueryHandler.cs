using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetAllLicense;

public class GetAllLicenseQueryHandler(ILicenseRepository repository, IMapper mapper, ICurrencyGrpc currencyGrpc) : IRequestHandler<GetAllLicenseQuery, Pagination<LicenseDto>>
{
    public async Task<Pagination<LicenseDto>> Handle(GetAllLicenseQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var licenses = await repository.MatchingAsync<LicenseAggregate>(request.Criteria, cancellationToken);


        var licensesDto = new List<LicenseDto>();

        foreach (var license in licenses.Data)
        {
            var licenseDto = mapper.Map<LicenseDto>(license);

            foreach (var price in license.Prices)
            {
                var currency = await currencyGrpc.GetCurrencyAsync(new gRpc.Clients.Services.Currencies.GetCurrencyRequest { Code = price.BasePrice.Currency }, cancellationToken);

                licenseDto.Prices.Add(new PriceDto
                {
                    BasePrice = price.BasePrice.ToDecimal(currency.DecimalDigits),
                    DiscountPercentage = price.DiscountPercentage,
                    BillingModel = price.BillingModel,
                    BillingType = price.BillingType,
                    Currency = price.BasePrice.Currency,
                    TaxPercentage = price.TaxPercentage
                });
            }

            licensesDto.Add(licenseDto);
        }

        return Pagination<LicenseDto>.Create(licensesDto, licenses.TotalCount, licenses.Limit, licenses.Skip);

    }
}
