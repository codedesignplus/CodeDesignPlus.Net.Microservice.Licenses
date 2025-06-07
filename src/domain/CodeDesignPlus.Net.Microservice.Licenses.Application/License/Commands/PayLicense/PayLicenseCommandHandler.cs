namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.PayLicense;

public class PayLicenseCommandHandler(ILicenseRepository repository, IUserContext user, IPubSub pubsub) : IRequestHandler<PayLicenseCommand>
{
    public Task Handle(PayLicenseCommand request, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}