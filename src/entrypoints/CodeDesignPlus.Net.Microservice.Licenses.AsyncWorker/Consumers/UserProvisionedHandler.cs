using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.CompleteProvisioningStep;
using CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.DomainEvents;
using MediatR;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Consumers;

[QueueName<OrderAggregate>("UserProvisionedHandler")]
public class UserProvisionedHandler(IMediator mediator, ILogger<UserProvisionedHandler> logger)
    : IEventHandler<UserProvisionedForOrderDomainEvent>
{
    public async Task HandleAsync(UserProvisionedForOrderDomainEvent data, CancellationToken cancellationToken)
    {
        logger.LogInformation("User {UserId} provisioned for Order {OrderId}", data.AggregateId, data.OrderId);
        await mediator.Send(new CompleteProvisioningStepCommand(data.OrderId, "UserProvisioning"), cancellationToken);
    }
}
