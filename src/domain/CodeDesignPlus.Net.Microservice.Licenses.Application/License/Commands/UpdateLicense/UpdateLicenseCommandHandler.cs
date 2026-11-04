using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;

public class UpdateLicenseCommandHandler(ILicenseRepository repository, IOrderRepository orders, IUserContext user, IPubSub pubsub, IMapper mapper, ICacheManager cacheManager, ICurrencyGrpc currencyGrpc) : IRequestHandler<UpdateLicenseCommand>
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

        // No se retira la oferta a quien ya tomó la licencia o la está pagando (pendings/464, decisión del usuario 2026-11-04).
        var withdrawn = license.OffersWithdrawnBy(request.IsActive, request.ShowInLandingPage, (request.OfferedTo ?? []).Select(x => x.TenantId));

        await LicenseOffers.EnsureCanWithdrawAsync(license.Id, withdrawn, orders, cancellationToken);

        license.Update(request.Name, request.ShortDescription, request.Description, modules, prices, request.Icon, request.TermsOfService, request.Attributes, request.IsActive, request.IsPopular, request.ShowInLandingPage, request.InvoiceConcept, request.OfferedTo ?? [], user.IdUser);

        await LicenseOffers.EnsureEachIsAnUpgradeAsync(license, orders, cancellationToken);

        await repository.UpdateAsync(license, cancellationToken);

        await pubsub.PublishAsync(license.GetAndClearEvents(), cancellationToken);

        await cacheManager.RemoveAsync(request.Id.ToString());
    }

    private async Task<List<Price>> GetPricesAsync(List<PriceInput> data, CancellationToken cancellationToken)
    {
        var prices = new List<Price>();

        foreach (var price in data)
        {
            var currency = await currencyGrpc.GetCurrencyAsync(code: price.BasePrice.Currency, cancellationToken: cancellationToken);

            prices.Add(price.ToPrice((short)currency.DecimalDigits));
        }

        return prices;
    }
}