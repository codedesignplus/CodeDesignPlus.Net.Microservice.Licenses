using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;
using CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using MediatR;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Consumers;

[QueueName<OrderAggregate>("UpdateOrderHandler")]
public class UpdateOrderHandler(ILogger<UpdateOrderHandler> logger, IMediator mediator) : IEventHandler<PaymentResponseAssociatedDomainEvent>
{
    public async Task HandleAsync(PaymentResponseAssociatedDomainEvent data, CancellationToken cancellationToken)
    {
        logger.LogInformation("PaymentResponseAssociatedDomainEvent Recived, {Json}", JsonSerializer.Serialize(data));

        if(data.Module != PayOrderCommandHandler.MODULE)
        {
            logger.LogDebug("PaymentResponseAssociatedDomainEvent ignored, invalid module {Module}", data.Module);
            return;
        }

        try
        {
            await mediator.Send(new UpdateStateOrderCommand(data.ReferenceId, (PaymentStatus)data.Status), cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to update order state for {ReferenceId}. Order may already be in final state. Skipping.", data.ReferenceId);
        }
    }
}
