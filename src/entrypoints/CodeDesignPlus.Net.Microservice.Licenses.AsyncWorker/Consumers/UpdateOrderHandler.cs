using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;
using CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using MediatR;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Consumers;

[QueueName("Order", "UpdateOrderHandler")]
public class UpdateOrderHandler(ILogger<UpdateOrderHandler> logger, IMediator mediator) : IEventHandler<PaymentResponseAssociatedDomainEvent>
{
    public Task HandleAsync(PaymentResponseAssociatedDomainEvent data, CancellationToken cancellationToken)
    {
        logger.LogInformation("PaymentResponseAssociatedDomainEvent Recived, {Json}", JsonSerializer.Serialize(data));

        if(data.Module != PayOrderCommandHandler.MODULE)
        {
            logger.LogDebug("PaymentResponseAssociatedDomainEvent ignored, invalid module {Module}", data.Module);
            return Task.CompletedTask;
        }

        return mediator.Send(new UpdateStateOrderCommand(data.ReferenceId, (PaymentStatus)data.Status), cancellationToken);
    }
}
