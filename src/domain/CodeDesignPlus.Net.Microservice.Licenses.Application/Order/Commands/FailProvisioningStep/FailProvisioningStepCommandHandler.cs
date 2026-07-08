using CodeDesignPlus.Net.gRpc.Clients.Abstractions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.FailProvisioningStep;

public class FailProvisioningStepCommandHandler(
    IOrderRepository orderRepository,
    IPubSub pubsub,
    INotificationGrpc notification
) : IRequestHandler<FailProvisioningStepCommand>
{
    public async Task Handle(FailProvisioningStepCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var order = await orderRepository.FindAsync<OrderAggregate>(request.OrderId, cancellationToken);

        if (order is null)
            return;

        order.FailProvisioningStep(request.StepName, request.Error, order.Buyer.BuyerId);

        await orderRepository.UpdateAsync(order, cancellationToken);
        await pubsub.PublishAsync(order.GetAndClearEvents(), cancellationToken);

        await notification.SendToUserAsync(new CodeDesignPlus.Net.gRpc.Clients.Services.Notification.NotificationUserRequest
        {
            UserId = order.Buyer.BuyerId.ToString(),
            EventName = "OrderProvisioningFailed",
            Id = order.Id.ToString(),
            SentBy = order.Buyer.BuyerId.ToString(),
            Tenant = order.TenantDetail.Id.ToString(),
            JsonPayload = CodeDesignPlus.Net.Serializers.JsonSerializer.Serialize(new
            {
                orderId = order.Id,
                stepName = request.StepName,
                message = $"Provisioning step '{request.StepName}' failed: {request.Error}"
            })
        }, cancellationToken);
    }
}
