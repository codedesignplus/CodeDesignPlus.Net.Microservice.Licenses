using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;
using CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;
using MediatR;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Consumers;

[QueueName<OrderAggregate>("UpdateOrderHandler")]
public class UpdateOrderHandler(ILogger<UpdateOrderHandler> logger, IMediator mediator, IOrderRepository orderRepository) : IEventHandler<PaymentResponseAssociatedDomainEvent>
{
    public async Task HandleAsync(PaymentResponseAssociatedDomainEvent data, CancellationToken cancellationToken)
    {
        logger.LogInformation("PaymentResponseAssociatedDomainEvent Recived, {Json}", JsonSerializer.Serialize(data));

        if(data.Module != PayOrderCommandHandler.MODULE)
        {
            logger.LogDebug("PaymentResponseAssociatedDomainEvent ignored, invalid module {Module}", data.Module);
            return;
        }

        var exists = await orderRepository.ExistsAsync<OrderAggregate>(data.ReferenceId, cancellationToken);

        if (!exists)
        {
            logger.LogInformation("Order {Id} not found. Skipping update.", data.ReferenceId);
            return;
        }

        await mediator.Send(new UpdateStateOrderCommand(data.ReferenceId, (PaymentStatus)data.Status), cancellationToken);
    }
}
