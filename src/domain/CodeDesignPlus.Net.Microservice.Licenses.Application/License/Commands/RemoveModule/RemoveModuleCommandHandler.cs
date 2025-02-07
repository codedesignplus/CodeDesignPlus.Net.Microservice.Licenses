namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.RemoveModule;

public class RemoveModuleCommandHandler(ILicenseRepository repository, IUserContext user, IPubSub pubsub) : IRequestHandler<RemoveModuleCommand>
{
    public async Task Handle(RemoveModuleCommand request, CancellationToken cancellationToken)
    {        
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var license = await repository.FindAsync<LicenseAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsNull(license, Errors.LicenseNotFound);

        license.RemoveModule(request.IdModule, user.IdUser);

        await repository.UpdateAsync(license, cancellationToken);

        await pubsub.PublishAsync(license.GetAndClearEvents(), cancellationToken);
    }
}