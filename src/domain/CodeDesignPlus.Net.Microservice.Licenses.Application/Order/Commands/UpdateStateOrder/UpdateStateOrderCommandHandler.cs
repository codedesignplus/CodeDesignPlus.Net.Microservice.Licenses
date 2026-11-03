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
    ISubscriptionSettlement settlement,
    ILogger<UpdateStateOrderCommandHandler> logger
) : IRequestHandler<UpdateStateOrderCommand>
{
    /// <summary>El target de ms-filestorage de los recibos de compra.</summary>
    public const string ReceiptTarget = "licenses-pdf";

    public async Task Handle(UpdateStateOrderCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var order = await orderRepository.FindAsync<OrderAggregate>(request.ReferenceId, cancellationToken);
        ApplicationGuard.IsNull(order, Errors.OrderNotFound);

        var previousStatus = order.PaymentStatus;

        // Una reentrega del mismo resultado no cambia nada, y no debe regenerar el recibo ni reenviar el correo. Lo que
        // sigue al pago de una renovación o un cambio sí se repasa: es idempotente, y si la entrega anterior se cayó a
        // mitad, aquí se completa.
        if (request.PaymentStatus == previousStatus)
        {
            await SettleAsync(order, cancellationToken);

            return;
        }

        order.SetPaymentStatus(request.PaymentStatus, order.Buyer.BuyerId, await PeriodStartAsync(order, request.PaymentStatus, cancellationToken));

        // Escritura condicional: si el webhook y la conciliacion aplican el mismo pago a la vez, solo uno gana, y
        // solo ese publica el aprovisionamiento. Nunca UpdateAsync: el documento entero pisaria los pasos que
        // ms-tenants y ms-users marcan en cuanto sale el evento (pendings/080).
        if (!await orderRepository.ApplyPaymentStatusAsync(order, previousStatus, cancellationToken))
            return;

        await pubsub.PublishAsync(order.GetAndClearEvents(), cancellationToken);

        await SettleAsync(order, cancellationToken);

        if (request.PaymentStatus == PaymentStatus.Succeeded && order.SupersededBy is not null)
        {
            // Lo pagado quedó como saldo a favor: ni recibo de licencia ni correo de renovación.
            return;
        }

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
                var variables = ReceiptEmailVariables.Build(order, currency);
                var tenant = order.TenantDetail.Id;

                var pdfRequest = new GeneratePdfRequest
                {
                    // Una renovación o un cambio muestran el crédito aplicado y lo pagado, no solo el precio (pendings/738).
                    TemplateType = ReceiptEmailVariables.ReceiptTemplateOf(order),
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
                        // El nombre dice qué se pagó: una renovación ya no llega como «PurchaseReceipt.pdf» (pendings/738).
                        FileName = ReceiptText.ReceiptFileName(order),
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

                    var (template, emailVariables) = order.Type == OrderType.Renewal
                        ? ("SubscriptionRenewed", RenewalEmailVariables.Build(order, currency))
                        : ("PurchaseConfirmation", variables);

                    var sendEmailEvent = new SendEmailDomainEvent(
                        Guid.NewGuid(),
                        templateName: template,
                        to: Recipients(order),
                        cc: [],
                        bcc: [],
                        variables: emailVariables,
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

    /// <summary>
    /// Desde cuándo paga la orden si este resultado la aprueba: una renovación pagada por adelantado empieza donde termina
    /// lo ya pagado, no hoy (pendings/735); lo demás empieza al pagarse.
    /// </summary>
    private async Task<Instant?> PeriodStartAsync(OrderAggregate order, PaymentStatus status, CancellationToken cancellationToken)
    {
        if (status != PaymentStatus.Succeeded || order.Type != OrderType.Renewal || order.SupersededBy is not null)
        {
            return null;
        }

        var now = SystemClock.Instance.GetCurrentInstant();
        var paid = await orderRepository.FindPaidByTenantAsync(order.TenantDetail.Id, cancellationToken);
        var subscription = Domain.ServiceDomain.SubscriptionTimeline.From(paid.Where(x => x.Id != order.Id));

        return subscription is not null && subscription.PaidThrough > now ? subscription.PaidThrough : now;
    }

    private Task SettleAsync(OrderAggregate order, CancellationToken cancellationToken)
    {
        if (order.Type == OrderType.Purchase)
        {
            return Task.CompletedTask;
        }

        return order.PaymentStatus switch
        {
            PaymentStatus.Succeeded => settlement.SettleAsync(order, cancellationToken),
            PaymentStatus.Failed or PaymentStatus.Expired => settlement.ReleaseAsync(order, cancellationToken),
            _ => Task.CompletedTask,
        };
    }

    /// <summary>
    /// A quién va el correo: a quien pagó y, en una renovación o un cambio, también al correo de la copropiedad.
    /// </summary>
    private static List<string> Recipients(OrderAggregate order) => order.Type == OrderType.Purchase
        ? [order.Buyer.Email]
        : [.. new[] { order.Buyer.Email, order.TenantDetail.Email }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)];
}
