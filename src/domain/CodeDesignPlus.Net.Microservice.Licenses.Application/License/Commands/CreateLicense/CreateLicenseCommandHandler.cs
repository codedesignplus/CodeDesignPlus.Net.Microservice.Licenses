using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.CreateLicense;

public class CreateLicenseCommandHandler(ILicenseRepository repository, IUserContext user, IPubSub pubsub, IMapper mapper) : IRequestHandler<CreateLicenseCommand>
{
    public async Task Handle(CreateLicenseCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);
        
        var exist = await repository.ExistsAsync<LicenseAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsTrue(exist, Errors.LicenseAlreadyExists);

        var licensePopularityExist = await repository.LicesePopularityExistsAsync(request.Id, cancellationToken);

        ApplicationGuard.IsTrue(licensePopularityExist, Errors.LicensePopularityAlreadyExists);

        var modules = mapper.Map<List<ModuleEntity>>(request.Modules);

        var license = LicenseAggregate.Create(request.Id, request.Name, request.ShortDescription, request.Description, modules, request.Prices, request.Icon, request.TermsOfService, request.Attributes, request.IsActive, request.IsPopular, user.IdUser);

        await repository.CreateAsync(license, cancellationToken);

        await pubsub.PublishAsync(license.GetAndClearEvents(), cancellationToken);
    }
}