namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.DeleteLicense;

public class DeleteLicenseCommandHandler(ILicenseRepository repository, IUserContext user, IPubSub pubsub) : IRequestHandler<DeleteLicenseCommand>
{
    public async Task Handle(DeleteLicenseCommand request, CancellationToken cancellationToken)
    {        
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var aggregate = await repository.FindAsync<LicenseAggregate>(request.Id, user.Tenant, cancellationToken);

        ApplicationGuard.IsNull(aggregate, Errors.LicenseNotFound);

        aggregate.Delete(user.IdUser);

        await repository.DeleteAsync<LicenseAggregate>(aggregate.Id, user.Tenant, cancellationToken);

        await pubsub.PublishAsync(aggregate.GetAndClearEvents(), cancellationToken);
    }
}