using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;

public class UpdateLicenseCommandHandler(ILicenseRepository repository, IUserContext user, IPubSub pubsub, IMapper mapper, ICacheManager cacheManager) : IRequestHandler<UpdateLicenseCommand>
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

        license.Update(request.Name, request.ShortDescription, request.Description, modules, request.Prices, request.Icon, request.TermsOfService, request.Attributes, request.IsActive, request.IsPopular, request.ShowInLandingPage, user.IdUser);

        await repository.UpdateAsync(license, cancellationToken);

        await pubsub.PublishAsync(license.GetAndClearEvents(), cancellationToken);

        await cacheManager.RemoveAsync(request.Id.ToString());
    }
}