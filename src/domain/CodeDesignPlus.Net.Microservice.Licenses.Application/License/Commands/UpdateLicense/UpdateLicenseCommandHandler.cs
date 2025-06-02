using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;

public class UpdateLicenseCommandHandler(ILicenseRepository repository, IUserContext user, IPubSub pubsub, IMapper mapper) : IRequestHandler<UpdateLicenseCommand>
{
    public async Task Handle(UpdateLicenseCommand request, CancellationToken cancellationToken)
    {        
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var license = await repository.FindAsync<LicenseAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsNull(license, Errors.LicenseNotFound);
        
        var licensePopularityExist = await repository.LicesePopularityExistsAsync(request.Id, cancellationToken);

        ApplicationGuard.IsTrue(licensePopularityExist, Errors.LicensePopularityAlreadyExists);

        var modules = mapper.Map<List<ModuleEntity>>(request.Modules);

        license.Update(request.Name, request.ShortDescription, request.Description, modules, request.Prices, request.Icon, request.TermsOfService, request.Attributes, request.IsActive, request.IsPopular, user.IdUser);

        await repository.UpdateAsync(license, cancellationToken);

        await pubsub.PublishAsync(license.GetAndClearEvents(), cancellationToken);
    }
}