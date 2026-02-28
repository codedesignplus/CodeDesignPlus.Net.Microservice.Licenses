using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;

public class UpdateLicenseCommandHandler(ILicenseRepository repository, IUserContext user, IPubSub pubsub, IMapper mapper, ICacheManager cacheManager, ICurrencyGrpc currencyGrpc) : IRequestHandler<UpdateLicenseCommand>
{
    public async Task Handle(UpdateLicenseCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var license = await repository.FindAsync<LicenseAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsNull(license, Errors.LicenseNotFound);

        if (request.IsPopular)
        {
            var licensePopularityExist = await repository.LicesePopularityExistsAsync(request.Id, cancellationToken);

            ApplicationGuard.IsTrue(licensePopularityExist, Errors.LicensePopularityAlreadyExists);
        }

        var modules = mapper.Map<List<ModuleEntity>>(request.Modules);

        var prices = await GetPricesAsync(request.Prices, cancellationToken);

        license.Update(request.Name, request.ShortDescription, request.Description, modules, prices, request.Icon, request.TermsOfService, request.Attributes, request.IsActive, request.IsPopular, request.ShowInLandingPage, user.IdUser);

        await repository.UpdateAsync(license, cancellationToken);

        await pubsub.PublishAsync(license.GetAndClearEvents(), cancellationToken);

        await cacheManager.RemoveAsync(request.Id.ToString());
    }

    private async Task<List<Price>> GetPricesAsync(List<PriceDto> data, CancellationToken cancellationToken)
    {
        var prices = new List<Price>();

        foreach (var price in data)
        {
            var currency = await currencyGrpc.GetCurrencyAsync(code: price.Currency, cancellationToken: cancellationToken);

            var decimalDigits = currency.DecimalDigits;

            var money = Money.FromDecimal(price.BasePrice, price.Currency, decimalDigits);

            prices.Add(Price.Create(price.BillingType, money, price.BillingModel, price.DiscountPercentage, price.TaxPercentage));
        }

        return prices;
    }
}