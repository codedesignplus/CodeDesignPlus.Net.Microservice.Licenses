using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Emails.gRpc;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;

public class UpdateStateOrderCommandHandler(
    IOrderRepository orderRepository,
    IPubSub pubsub,
    INotificationGrpc notification,
    IEmailGrpc emailGrpc
) : IRequestHandler<UpdateStateOrderCommand>
{
    public async Task Handle(UpdateStateOrderCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var order = await orderRepository.FindAsync<OrderAggregate>(request.ReferenceId, cancellationToken);
        ApplicationGuard.IsNull(order, Errors.OrderNotFound);

        order.SetPaymentStatus(request.PaymentStatus, order.Buyer.BuyerId);

        await orderRepository.UpdateAsync(order, cancellationToken);

        await pubsub.PublishAsync(order.GetAndClearEvents(), cancellationToken);

        var receiptUrl = string.Empty;

        if (request.PaymentStatus == PaymentStatus.Succeeded)
        {
            var variables = BuildVariables(order);

            var pdfRequest = new GeneratePdfRequest
            {
                TemplateType = "PurchaseReceipt",
                Tenant = order.TenantDetail.Id.ToString()
            };
            foreach (var kvp in variables)
                pdfRequest.Values.Add(kvp.Key, kvp.Value);

            var pdfResult = await emailGrpc.GeneratePdfAsync(pdfRequest, cancellationToken);

            if (pdfResult.Success)
            {
                receiptUrl = pdfResult.SignedUrl;

                var fileId = Guid.Parse(pdfResult.Id);
                var attachment = new FileAttachment(fileId, pdfResult.Name, pdfResult.Target);

                var sendEmailEvent = new SendEmailDomainEvent(
                    Guid.NewGuid(),
                    templateName: "PurchaseConfirmation",
                    to: [order.Buyer.Email],
                    cc: [],
                    bcc: [],
                    variables: variables,
                    attachments: [attachment],
                    tenant: order.TenantDetail.Id
                );

                await pubsub.PublishAsync(sendEmailEvent, cancellationToken);
            }
        }

        await notification.SendToUserAsync(new gRpc.Clients.Services.Notification.NotificationUserRequest
        {
            UserId = order.Buyer.BuyerId.ToString(),
            EventName = "OrderPaymentCompleted",
            Id = order.Id.ToString(),
            SentBy = order.Buyer.BuyerId.ToString(),
            Tenant = order.TenantDetail.Id.ToString(),
            JsonPayload = CodeDesignPlus.Net.Serializers.JsonSerializer.Serialize(new
            {
                receiptUrl
            })
        }, cancellationToken);
    }

    private static Dictionary<string, string> BuildVariables(OrderAggregate order) => new()
    {
        ["organization_name"] = order.TenantDetail.Name,
        ["organization_email"] = order.TenantDetail.Email,
        ["organization_phone"] = order.TenantDetail.Phone,
        ["organization_document"] = $"{order.TenantDetail.TypeDocument}: {order.TenantDetail.NumberDocument}",
        ["buyer_name"] = order.Buyer.Name,
        ["buyer_email"] = order.Buyer.Email,
        ["license_name"] = order.License.Name,
        ["billing_type"] = order.License.BillingType.ToString(),
        ["subtotal"] = order.License.SubTotal.Amount.ToString("N2"),
        ["tax"] = order.License.Tax.Amount.ToString("N2"),
        ["total"] = order.License.Total.Amount.ToString("N2"),
        ["currency"] = order.License.Total.Currency,
        ["purchase_date"] = SystemClock.Instance.GetCurrentInstant().ToString(),
        ["current_year"] = DateTime.UtcNow.Year.ToString(),
        ["order_id"] = order.Id.ToString(),
        ["modules_list"] = string.Join(", ", order.License.Modules.Select(m => m.Name))
    };
}