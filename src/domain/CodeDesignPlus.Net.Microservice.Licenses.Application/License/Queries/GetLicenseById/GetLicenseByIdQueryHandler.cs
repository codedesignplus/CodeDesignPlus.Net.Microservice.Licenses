using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetLicenseById;

public class GetLicenseByIdQueryHandler(ILicenseRepository repository, IMapper mapper, ICacheManager cacheManager, ICurrencyGrpc currencyGrpc) : IRequestHandler<GetLicenseByIdQuery, LicenseDto>
{
    public async Task<LicenseDto> Handle(GetLicenseByIdQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var exists = await cacheManager.ExistsAsync(request.Id.ToString());

        if (exists)
            return await cacheManager.GetAsync<LicenseDto>(request.Id.ToString());

        var license = await repository.FindAsync<LicenseAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsNull(license, Errors.LicenseNotFound);

        var dto = mapper.Map<LicenseDto>(license);

        foreach (var price in license.Prices)
        {
            var currency = await currencyGrpc.GetCurrencyAsync(code: price.BasePrice.Currency, cancellationToken: cancellationToken);

            dto.Prices.Add(new PriceDto
            {
                BasePrice = price.BasePrice.ToDecimal(currency.DecimalDigits),
                DiscountPercentage = price.DiscountPercentage,
                BillingModel = price.BillingModel,
                BillingType = price.BillingType,
                Currency = price.BasePrice.Currency,
                TaxPercentage = price.TaxPercentage
            });
        };


        await cacheManager.SetAsync(request.Id.ToString(), dto);

        return dto;
    }
}
