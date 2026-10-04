using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Emails.gRpc;
using CodeDesignPlus.Net.gRpc.Clients.Services.FileStorage;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;

public class UpdateStateOrderCommandHandler(
    IOrderRepository orderRepository,
    IPubSub pubsub,
    ILiveChannelGrpc liveChannel,
    IEmailGrpc emailGrpc,
    IFileStorageGrpc fileStorage,
    ICurrencyGrpc currencyGrpc,
    ILogger<UpdateStateOrderCommandHandler> logger
) : IRequestHandler<UpdateStateOrderCommand>
{
    /// <summary>El target de ms-filestorage de los recibos de compra.</summary>
    public const string ReceiptTarget = "licenses-pdf";

    /// <summary>El nombre del recibo: cada uno vive en su carpeta (licenses-pdf/{id}/), no necesita llevar el id.</summary>
    public const string ReceiptFileName = "PurchaseReceipt.pdf";

    public async Task Handle(UpdateStateOrderCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var order = await orderRepository.FindAsync<OrderAggregate>(request.ReferenceId, cancellationToken);
        ApplicationGuard.IsNull(order, Errors.OrderNotFound);

        var previousStatus = order.PaymentStatus;

        order.SetPaymentStatus(request.PaymentStatus, order.Buyer.BuyerId);

        // Una reentrega del mismo resultado no cambia nada, y no debe regenerar el recibo ni reenviar el correo.
        if (order.PaymentStatus == previousStatus)
            return;

        // Escritura condicional: si el webhook y la conciliacion aplican el mismo pago a la vez, solo uno gana, y
        // solo ese publica el aprovisionamiento. Nunca UpdateAsync: el documento entero pisaria los pasos que
        // ms-tenants y ms-users marcan en cuanto sale el evento (pendings/080).
        if (!await orderRepository.ApplyPaymentStatusAsync(order, previousStatus, cancellationToken))
            return;

        await pubsub.PublishAsync(order.GetAndClearEvents(), cancellationToken);

        if (request.PaymentStatus == PaymentStatus.Succeeded)
        {
            try
            {
                // Efimero: la fase 1 hizo consultable la compra. /purchase/processing reconcilia
                // contra GET Order/{id}, que ya trae PaymentStatus, ProvisioningStatus y el recibo.
                await liveChannel.PushToUserAsync(
                    order.Buyer.BuyerId,
                    "OrderPaymentSucceeded",
                    new
                    {
                        orderId = order.Id,
                        status = "PaymentSucceeded"
                    },
                    order.TenantDetail.Id,
                    cancellationToken: cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to send SignalR notification for Order {OrderId}. Non-critical.", order.Id);
            }

            try
            {
                var currency = await currencyGrpc.GetCurrencyAsync(code: order.License.Total.Currency, cancellationToken: cancellationToken);
                var variables = ReceiptEmailVariables.Build(order, currency.DecimalDigits);
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
                    // Por ms-filestorage y no directo al blob: así queda en licenses-pdf/{id}/ con su registro, que es lo
                    // que permite darlo de baja y descargarlo con sus permisos (pendings/260, regla 56).
                    var stored = await fileStorage.UploadAsync(new UploadFileRequest
                    {
                        Id = Guid.NewGuid().ToString(),
                        Tenant = tenant.ToString(),
                        UploadedBy = order.Buyer.BuyerId.ToString(),
                        Target = ReceiptTarget,
                        FileName = ReceiptFileName,
                        Content = pdfResult.PdfContent,
                    }, cancellationToken);

                    var fileId = Guid.Parse(stored.Id);
                    var fileName = stored.FileName;
                    var target = stored.Target;

                    var attachment = new FileAttachment(fileId, fileName, target);

                    // La referencia se guarda en la orden, no la URL firmada: la firma caduca a los 7 dias.
                    // Antes solo viajaba adjunta al correo y aqui se calculaba una `receiptUrl` que nadie leia,
                    // asi que la pantalla de compra no tenia de donde sacar el recibo.
                    order.AttachReceipt(fileId, fileName, target, order.Buyer.BuyerId);
                    // Solo el recibo: a estas alturas los pasos de aprovisionamiento ya se estan marcando.
                    await orderRepository.AttachReceiptAsync(order.Id, order.Receipt!, order.Buyer.BuyerId, cancellationToken);

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
}
