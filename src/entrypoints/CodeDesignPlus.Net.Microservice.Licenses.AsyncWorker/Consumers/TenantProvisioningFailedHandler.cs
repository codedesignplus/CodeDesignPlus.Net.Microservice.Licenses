using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.FailProvisioningStep;
using CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.DomainEvents;
using MediatR;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Consumers;

[QueueName<OrderAggregate>("TenantProvisioningFailedHandler")]
public class TenantProvisioningFailedHandler(IMediator mediator, ILogger<TenantProvisioningFailedHandler> logger)
    : IEventHandler<TenantProvisioningFailedForOrderDomainEvent>
{
    public async Task HandleAsync(TenantProvisioningFailedForOrderDomainEvent data, CancellationToken cancellationToken)
    {
        logger.LogWarning("Tenant provisioning failed for Order {OrderId}: {Reason}", data.OrderId, data.Reason);
        await mediator.Send(new FailProvisioningStepCommand(data.OrderId, "TenantProvisioning", data.Reason), cancellationToken);
    }
}
