using CodeDesignPlus.Net.gRpc.Clients.Abstractions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.FailProvisioningStep;

/// <summary>
/// Marca un paso del aprovisionamiento como fallido y deja el pedido en <c>PartiallyFailed</c>.
/// </summary>
/// <remarks>
/// <b>Esto esta vivo.</b> Lo llaman los dos consumidores de aprovisionamiento fallido, y los eventos que
/// escuchan se publican de verdad: ms-tenants emite el suyo en <c>CreateTenantHandler</c> cuando falla la
/// creacion de la copropiedad, y ms-users el suyo en <c>CompleteOrderHandler</c>.
/// <para>
/// Se deja escrito porque <b>ya se dieron por muertos una vez</b>: el guardarrail de gemelos los reporta como
/// colgados, y no lo estan. La causa es que ms-tenants declara su evento en la carpeta <c>DomainEvents</c>
/// del <c>AsyncWorker</c> en vez de en su capa de dominio, y el guardarrail entiende eso como un gemelo sin
/// publicador. Es un falso positivo del detector, no un consumidor huerfano.
/// </para>
/// <para>
/// El otro camino hasta aqui es <c>OrderReconciliationJob</c>, que llama a este mismo comando al agotar los
/// cinco intentos de reaprovisionamiento. Uno rinde por fallo declarado y el otro por insistencia inutil.
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
