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

        var decimalDigitsByCurrency = await GetDecimalDigitsByCurrencyAsync(request, cancellationToken);

        var prices = request.Prices.ConvertAll(x =>
        {
            var decimalDigits = decimalDigitsByCurrency[x.Currency];

            var money = Money.FromDecimal(x.BasePrice, x.Currency, decimalDigits);

            return Price.Create(x.BillingType, money, x.BillingModel, x.DiscountPercentage, x.TaxPercentage);
        });

        license.Update(request.Name, request.ShortDescription, request.Description, modules, prices, request.Icon, request.TermsOfService, request.Attributes, request.IsActive, request.IsPopular, request.ShowInLandingPage, user.IdUser);

        await repository.UpdateAsync(license, cancellationToken);

        await pubsub.PublishAsync(license.GetAndClearEvents(), cancellationToken);

        await cacheManager.RemoveAsync(request.Id.ToString());
    }

    private async Task<Dictionary<string, short>> GetDecimalDigitsByCurrencyAsync(UpdateLicenseCommand request, CancellationToken cancellationToken)
    {
        var uniqueCurrencyCodes = request.Prices.Select(p => p.Currency).Distinct().ToList();
        
        var decimalDigitsByCurrency = new Dictionary<string, short>();

        foreach (var code in uniqueCurrencyCodes)
        {
            var currency = await currencyGrpc.GetCurrencyAsync(code: code, cancellationToken: cancellationToken);

            decimalDigitsByCurrency[code] = currency.DecimalDigits;
        }

        return decimalDigitsByCurrency;
    }
}