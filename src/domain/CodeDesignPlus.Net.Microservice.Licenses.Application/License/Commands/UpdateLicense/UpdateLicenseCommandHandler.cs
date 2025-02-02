namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;

public class UpdateLicenseCommandHandler(ILicenseRepository repository, IUserContext user, IPubSub pubsub) : IRequestHandler<UpdateLicenseCommand>
{
    public Task Handle(UpdateLicenseCommand request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}