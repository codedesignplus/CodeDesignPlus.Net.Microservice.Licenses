using CodeDesignPlus.Net.File.Storage.Abstractions;
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
    IEmailGrpc emailGrpc,
    IFileStorage fileStorage,
    ICurrencyGrpc currencyGrpc,
    ILogger<UpdateStateOrderCommandHandler> logger
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

        if (request.PaymentStatus == PaymentStatus.Succeeded)
        {
            try
            {
                await notification.SendToUserAsync(new gRpc.Clients.Services.Notification.NotificationUserRequest
                {
                    UserId = order.Buyer.BuyerId.ToString(),
                    EventName = "OrderPaymentSucceeded",
                    Id = order.Id.ToString(),
                    SentBy = order.Buyer.BuyerId.ToString(),
                    Tenant = order.TenantDetail.Id.ToString(),
                    JsonPayload = CodeDesignPlus.Net.Serializers.JsonSerializer.Serialize(new
                    {
                        orderId = order.Id,
                        status = "PaymentSucceeded"
                    })
                }, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send SignalR notification for Order {OrderId}. Non-critical.", order.Id);
            }

            try
            {
                var currency = await currencyGrpc.GetCurrencyAsync(code: order.License.Total.Currency, cancellationToken: cancellationToken);
                var variables = BuildVariables(order, currency.DecimalDigits);
                var tenant = order.TenantDetail.Id;

                var pdfRequest = new GeneratePdfRequest
                {
                    TemplateType = "PurchaseReceipt",
                    Tenant = tenant.ToString()
                };

                foreach (var kvp in variables)
                    pdfRequest.Values.Add(kvp.Key, kvp.Value);

                var pdfResult = await emailGrpc.GeneratePdfAsync(pdfRequest, cancellationToken);

                if (pdfResult.Success)
                {
                    var fileId = Guid.NewGuid();
                    var fileName = $"PurchaseReceipt-{fileId}.pdf";
                    var target = $"licenses-pdf/{tenant}";

                    using var stream = new MemoryStream(pdfResult.PdfContent.ToByteArray());
                    await fileStorage.UploadAsync(stream, fileName, target, false, tenant, cancellationToken);

                    var signedResponse = await fileStorage.GetSignedUrlAsync(fileName, target, TimeSpan.FromDays(7), tenant, cancellationToken);
                    var receiptUrl = signedResponse?.Success == true ? signedResponse.File.Detail.SignedUrl.ToString() : string.Empty;

                    var attachment = new FileAttachment(fileId, fileName, target);

                    var sendEmailEvent = new SendEmailDomainEvent(
                        Guid.NewGuid(),
                        templateName: "PurchaseConfirmation",
                        to: [order.Buyer.Email],
                        cc: [],
                        bcc: [],
                        variables: variables,
                        attachments: [attachment],
                        tenant: tenant
                    );

                    await pubsub.PublishAsync(sendEmailEvent, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to generate/send receipt email for Order {OrderId}. Non-critical.", order.Id);
            }
        }
    }

    private static Dictionary<string, string> BuildVariables(OrderAggregate order, short decimalDigits) => new()
    {
        ["organization_name"] = order.TenantDetail.Name,
        ["organization_email"] = order.TenantDetail.Email,
        ["organization_phone"] = order.TenantDetail.Phone,
        ["organization_document"] = $"{order.TenantDetail.TypeDocument}: {order.TenantDetail.NumberDocument}",
        ["buyer_name"] = order.Buyer.Name,
        ["buyer_email"] = order.Buyer.Email,
        ["license_name"] = order.License.Name,
        ["billing_type"] = order.License.BillingType.ToString(),
        ["subtotal"] = order.License.SubTotal.ToDecimal(decimalDigits).ToString("N2"),
        ["tax"] = order.License.Tax.ToDecimal(decimalDigits).ToString("N2"),
        ["total"] = order.License.Total.ToDecimal(decimalDigits).ToString("N2"),
        ["currency"] = order.License.Total.Currency,
        ["purchase_date"] = SystemClock.Instance.GetCurrentInstant().ToString(),
        ["current_year"] = DateTime.UtcNow.Year.ToString(),
        ["order_id"] = order.Id.ToString(),
        ["modules_list"] = string.Join(", ", order.License.Modules.Select(m => m.Name))
    };
}
