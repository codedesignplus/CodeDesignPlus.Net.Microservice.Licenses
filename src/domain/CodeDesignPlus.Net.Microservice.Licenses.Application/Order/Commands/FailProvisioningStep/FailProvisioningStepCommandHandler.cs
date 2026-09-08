using CodeDesignPlus.Net.gRpc.Clients.Abstractions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.FailProvisioningStep;

public class FailProvisioningStepCommandHandler(
    IOrderRepository orderRepository,
    IPubSub pubsub,
    ILiveChannelGrpc liveChannel
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

        // Efimero: la fase 1 hizo consultable la compra. /purchase/processing reconcilia contra
        // GET Order/{id}, que ya trae PaymentStatus, ProvisioningStatus y el recibo, asi que quien
        // cierre el navegador se entera igual al volver.
        await liveChannel.PushToUserAsync(
            order.Buyer.BuyerId,
            "OrderProvisioningFailed",
            new
            {
                orderId = order.Id,
                stepName = request.StepName,
                message = $"Provisioning step '{request.StepName}' failed: {request.Error}"
            },
            order.TenantDetail.Id,
            cancellationToken: cancellationToken);
    }
}
