using CodeDesignPlus.Net.gRpc.Clients.Abstractions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.FailProvisioningStep;

/// <summary>
/// Marca un paso del aprovisionamiento como fallido y deja el pedido en <c>PartiallyFailed</c>.
/// </summary>
/// <remarks>
/// <b>Hoy no lo llama nadie, y es a proposito.</b> Lo invocaban dos consumidores suscritos a eventos que
/// ningun micro publica —nunca se llegaron a construir— y se retiraron con el pendiente 52: una cola viva
/// sobre un camino muerto hace perder una tarde el dia que algo no llega.
/// <para>
/// La capacidad se conserva porque es la que usara la compensacion cuando se rehaga el motor de pagos: ahi
/// hay que decidir que pasa con el dinero cobrado de una compra que no se pudo entregar, y esa decision
/// depende del proveedor nuevo. Lo que <b>no</b> depende de el es que hoy
/// <c>OrderReconciliationJob</c> reintente el aprovisionamiento cada cinco minutos <b>sin rendirse nunca</b>:
/// sin contador de intentos ni escalado, un fallo permanente se reintenta para siempre y nadie se entera.
/// </para>
/// <para>
/// Cuidado al retirarla del todo: <c>ProvisioningStatus.PartiallyFailed</c> y
/// <c>ProvisioningStepStatus.Failed</c> son estado persistido.
/// </para>
/// </remarks>
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
