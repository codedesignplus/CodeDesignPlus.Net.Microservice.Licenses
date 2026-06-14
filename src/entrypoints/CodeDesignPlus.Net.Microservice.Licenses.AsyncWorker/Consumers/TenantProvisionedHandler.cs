using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.CompleteProvisioningStep;
using CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.DomainEvents;
using MediatR;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Consumers;

[QueueName<OrderAggregate>("TenantProvisionedHandler")]
public class TenantProvisionedHandler(IMediator mediator, ILogger<TenantProvisionedHandler> logger)
    : IEventHandler<TenantProvisionedForOrderDomainEvent>
{
    public async Task HandleAsync(TenantProvisionedForOrderDomainEvent data, CancellationToken cancellationToken)
    {
        logger.LogInformation("Tenant {TenantId} provisioned for Order {OrderId}", data.AggregateId, data.OrderId);
        await mediator.Send(new CompleteProvisioningStepCommand(data.OrderId, "TenantProvisioning"), cancellationToken);
    }
}
