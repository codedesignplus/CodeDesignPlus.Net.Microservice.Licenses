namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.CreateLicense;

public class CreateLicenseCommandHandler(ILicenseRepository repository, IUserContext user, IPubSub pubsub) : IRequestHandler<CreateLicenseCommand>
{
    public Task Handle(CreateLicenseCommand request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}