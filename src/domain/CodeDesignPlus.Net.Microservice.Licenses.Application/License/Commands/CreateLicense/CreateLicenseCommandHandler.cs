using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.CreateLicense;

public class CreateLicenseCommandHandler(ILicenseRepository repository, IUserContext user, IPubSub pubsub, IMapper mapper, ICurrencyGrpc currencyGrpc) : IRequestHandler<CreateLicenseCommand>
{
    public async Task Handle(CreateLicenseCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var exist = await repository.ExistsAsync<LicenseAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsTrue(exist, Errors.LicenseAlreadyExists);

        if (request.IsPopular)
        {
            var licensePopularityExist = await repository.LicesePopularityExistsAsync(request.Id, cancellationToken);

            ApplicationGuard.IsTrue(licensePopularityExist, Errors.LicensePopularityAlreadyExists);
        }

        var modules = mapper.Map<List<ModuleEntity>>(request.Modules);

        var prices = await GetPricesAsync(request.Prices, cancellationToken);

        var license = LicenseAggregate.Create(request.Id, request.Name, request.ShortDescription, request.Description, modules, prices, request.Icon, request.TermsOfService, request.Attributes, request.IsActive, request.IsPopular, request.ShowInLandingPage, user.IdUser);

        await repository.CreateAsync(license, cancellationToken);

        await pubsub.PublishAsync(license.GetAndClearEvents(), cancellationToken);
    }

    private async Task<List<Price>> GetPricesAsync(List<PriceInput> data, CancellationToken cancellationToken)
    {
        var prices = new List<Price>();

        foreach (var price in data)
        {
            var currency = await currencyGrpc.GetCurrencyAsync(code: price.BasePrice.Currency, cancellationToken: cancellationToken);

            var money = price.BasePrice.ToMoney(currency.DecimalDigits);

            prices.Add(Price.Create(price.BillingType, money, price.BillingModel, BasisPoints.FromPercentage(price.DiscountPercentage), BasisPoints.FromPercentage(price.TaxPercentage)));
        }

        return prices;
    }
}