namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.AddModule;

public class AddModuleCommandHandler(ILicenseRepository repository, IUserContext user, IPubSub pubsub) : IRequestHandler<AddModuleCommand>
{
    public async Task Handle(AddModuleCommand request, CancellationToken cancellationToken)
    {        
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var license = await repository.FindAsync<LicenseAggregate>(request.Id, user.Tenant, cancellationToken);

        ApplicationGuard.IsNull(license, Errors.LicenseNotFound);

        license.AddModule(request.IdModule, request.Name, user.IdUser);

        await repository.UpdateAsync(license, cancellationToken);

        await pubsub.PublishAsync(license.GetAndClearEvents(), cancellationToken);
    }
}