using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;

public class UpdateLicenseCommandHandler(ILicenseRepository repository, IUserContext user, IPubSub pubsub, IMapper mapper) : IRequestHandler<UpdateLicenseCommand>
{
    public async Task Handle(UpdateLicenseCommand request, CancellationToken cancellationToken)
    {        
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var license = await repository.FindAsync<LicenseAggregate>(request.Id, user.Tenant, cancellationToken);

        ApplicationGuard.IsNull(license, Errors.LicenseNotFound);

        var modules = mapper.Map<List<ModuleEntity>>(request.Modules);

        license.Update(request.Name, request.Description, modules, request.BillingType, request.Currency, request.Price, request.Attributes, user.IdUser);

        await repository.UpdateAsync(license, cancellationToken);

        await pubsub.PublishAsync(license.GetAndClearEvents(), cancellationToken);
    }
}