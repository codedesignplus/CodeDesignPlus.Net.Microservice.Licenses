using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
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

        var decimalDigitsByCurrency = await GetDecimalDigitsByCurrencyAsync(request, cancellationToken);

        var prices = request.Prices.ConvertAll(x =>
        {
            var decimalDigits = decimalDigitsByCurrency[x.Currency];

            var money = Money.FromDecimal(x.BasePrice, x.Currency, decimalDigits);

            return Price.Create(x.BillingType, money, x.BillingModel, x.DiscountPercentage, x.TaxPercentage);
        });

        var license = LicenseAggregate.Create(request.Id, request.Name, request.ShortDescription, request.Description, modules, prices, request.Icon, request.TermsOfService, request.Attributes, request.IsActive, request.IsPopular, request.ShowInLandingPage, user.IdUser);

        await repository.CreateAsync(license, cancellationToken);

        await pubsub.PublishAsync(license.GetAndClearEvents(), cancellationToken);
    }


    private async Task<Dictionary<string, short>> GetDecimalDigitsByCurrencyAsync(CreateLicenseCommand request, CancellationToken cancellationToken)
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