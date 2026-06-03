using CodeDesignPlus.Net.gRpc.Clients.Abstractions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;

public class UpdateStateOrderCommandHandler(IOrderRepository orderRepository, ILicenseRepository licenseRepository, IPubSub pubsub, INotificationGrpc notification) : IRequestHandler<UpdateStateOrderCommand>
{
    public async Task Handle(UpdateStateOrderCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var order = await orderRepository.FindAsync<OrderAggregate>(request.ReferenceId, cancellationToken);
        ApplicationGuard.IsNull(order, Errors.OrderNotFound);

        var license = await licenseRepository.FindAsync<LicenseAggregate>(order.License.Id, cancellationToken);
        ApplicationGuard.IsNull(license, Errors.LicenseNotFound);

        order.SetPaymentStatus(request.PaymentStatus, license.Attributes, order.Buyer.BuyerId, license.Modules);

        await orderRepository.UpdateAsync(order, cancellationToken);

        await pubsub.PublishAsync(order.GetAndClearEvents(), cancellationToken);

        await notification.SendToUserAsync(new gRpc.Clients.Services.Notification.NotificationUserRequest
        {
            UserId = order.Buyer.BuyerId.ToString(),
            EventName = "OrderPaymentCompleted",
            Id = order.Id.ToString(),
            SentBy = order.Buyer.BuyerId.ToString(),
            Tenant = order.TenantDetail.Id.ToString(),
            JsonPayload = CodeDesignPlus.Net.Serializers.JsonSerializer.Serialize(new
            {
                receiptUrl = ""
            })
        }, cancellationToken);
    }
}