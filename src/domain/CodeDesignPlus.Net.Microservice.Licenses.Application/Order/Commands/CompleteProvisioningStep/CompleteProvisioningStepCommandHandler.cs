using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.CompleteProvisioningStep;

public class CompleteProvisioningStepCommandHandler(
    IOrderRepository orderRepository,
    IPubSub pubsub,
    INotificationGrpc notification
) : IRequestHandler<CompleteProvisioningStepCommand>
{
    private static readonly string[] RequiredSteps = ["TenantProvisioning", "UserProvisioning"];

    public async Task Handle(CompleteProvisioningStepCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        // Atomic push: adds step to ProvisioningHistory only if not already completed (idempotent)
        // Returns the updated document or null if step was already completed
        var order = await orderRepository.CompleteProvisioningStepAtomicAsync(request.OrderId, request.StepName, cancellationToken);

        if (order is null)
            return;

        // Check if all required steps are now completed
        var completedSteps = order.ProvisioningHistory
            .Where(s => s.Status == ProvisioningStepStatus.Completed)
            .Select(s => s.StepName)
            .ToHashSet();

        if (!RequiredSteps.All(completedSteps.Contains))
            return;

        if (order.ProvisioningStatus == ProvisioningStatus.Completed)
            return;

        // All steps completed — mark order as Completed and notify
        order.SetProvisioningCompleted();

        await orderRepository.UpdateAsync(order, cancellationToken);
        await pubsub.PublishAsync(order.GetAndClearEvents(), cancellationToken);

        await notification.SendToUserAsync(new CodeDesignPlus.Net.gRpc.Clients.Services.Notification.NotificationUserRequest
        {
            UserId = order.Buyer.BuyerId.ToString(),
            EventName = "OrderFullyProvisioned",
            Id = order.Id.ToString(),
            SentBy = order.Buyer.BuyerId.ToString(),
            Tenant = order.TenantDetail.Id.ToString(),
            JsonPayload = CodeDesignPlus.Net.Serializers.JsonSerializer.Serialize(new
            {
                orderId = order.Id,
                tenantId = order.TenantDetail.Id
            })
        }, cancellationToken);
    }
}
