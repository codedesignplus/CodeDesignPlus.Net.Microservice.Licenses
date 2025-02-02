namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.DeleteLicense;

public class DeleteLicenseCommandHandler(ILicenseRepository repository, IUserContext user, IPubSub pubsub) : IRequestHandler<DeleteLicenseCommand>
{
    public Task Handle(DeleteLicenseCommand request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}