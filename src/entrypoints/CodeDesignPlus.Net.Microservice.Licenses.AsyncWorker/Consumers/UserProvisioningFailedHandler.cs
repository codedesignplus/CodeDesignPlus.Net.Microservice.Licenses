using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.FailProvisioningStep;
using CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.DomainEvents;
using MediatR;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Consumers;

[QueueName<OrderAggregate>("UserProvisioningFailedHandler")]
public class UserProvisioningFailedHandler(IMediator mediator, ILogger<UserProvisioningFailedHandler> logger)
    : IEventHandler<UserProvisioningFailedForOrderDomainEvent>
{
    public async Task HandleAsync(UserProvisioningFailedForOrderDomainEvent data, CancellationToken cancellationToken)
    {
        logger.LogWarning("User provisioning failed for Order {OrderId}: {Reason}", data.OrderId, data.Reason);
        await mediator.Send(new FailProvisioningStepCommand(data.OrderId, "UserProvisioning", data.Reason), cancellationToken);
    }
}
