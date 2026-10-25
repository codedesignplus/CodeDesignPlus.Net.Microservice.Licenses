using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.Payment;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;

public class PayOrderCommandHandler(
    ILicenseRepository repository,
    IUserContext user,
    IPubSub pubsub,
    IMapper mapper,
    IPaymentGrpc paymentGrpc,
    ITenantGrpc tenantGrpc,
    IOrderRepository orders
) : IRequestHandler<PayOrderCommand, PaymentResponse>
{
    public const string MODULE = "Licenses";

    public async Task<PaymentResponse> Handle(PayOrderCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        // Ya guardada por un envío anterior del mismo checkout: el reintento recibe la misma respuesta del cobro y no se cobra
        // otra vez (pendings/589, regla 66). Antes respondía «ya existe», y la pantalla no sabía qué pasó con el pago. La orden
        // es de quien la creó: otro usuario con ese id sigue recibiendo «ya existe».
        var stored = await orders.FindAsync<OrderAggregate>(request.Id, cancellationToken);

        if (stored is not null)
        {
            ApplicationGuard.IsTrue(stored.CreatedBy != user.IdUser, Errors.OrderAlreadyExists);

            return ResponseOf(stored);
        }

        var licenseAggregate = await repository.FindAsync<LicenseAggregate>(request.License.Id, cancellationToken);
        ApplicationGuard.IsNull(licenseAggregate, Errors.LicenseNotFound);

        // Una licencia retirada de la venta no se compra, aunque alguien conserve su id (plan 056 de pendings/).
        ApplicationGuard.IsFalse(licenseAggregate.IsActive, Errors.LicenseIsNotActive);

        var existTenant = await tenantGrpc.ExistTenantAsync(request.TenantDetail.Id, cancellationToken);
        ApplicationGuard.IsTrue(existTenant, Errors.TenantAlreadyExists);

        var price = licenseAggregate.Prices.FirstOrDefault(x => x.BillingType == request.License.BillingType);
        ApplicationGuard.IsNull(price, Errors.PriceNotFoundBecauseBillingTypeIsNotAvailableInTheLicense);

        var buyer = Net.ValueObjects.User.Buyer.Create(user.IdUser, request.Buyer.Name, request.Buyer.Phone, request.Buyer.Email, request.Buyer.TypeDocument, request.Buyer.Document, request.Buyer.ShippingAddress);

        var license = Domain.ValueObjects.License.Create(
            licenseAggregate.Id,
            licenseAggregate.Name,
            price.Total,
            price.Tax,
            price.SubTotal,
            price.BillingType,
            price.BillingModel,
            licenseAggregate.Description,
            licenseAggregate.ShortDescription,
            licenseAggregate.Icon,
            licenseAggregate.TermsOfService,
            licenseAggregate.IsPopular,
            licenseAggregate.ShowInLandingPage,
            licenseAggregate.Attributes,
            licenseAggregate.Modules.Select(m => new Domain.ValueObjects.LicenseModule(m.Id, m.Name, m.Description)).ToList()
        );

        var order = OrderAggregate.Create(request.Id, Guid.NewGuid(), license, request.PaymentMethod, buyer, request.TenantDetail, user.IdUser);

        // Dos envíos que llegan a la vez pasan los dos la comprobación de arriba; el segundo choca contra el id y no cobra ni
        // publica: devuelve lo que haya guardado el primero.
        if (!await orders.CreateIfAbsentAsync(order, cancellationToken))
            return ResponseOf(await orders.FindAsync<OrderAggregate>(order.Id, cancellationToken) ?? order);

        var responseGrpc = await PayLicenseAsync(order, licenseAggregate, request.TenantDetail.Location.Country.Currency, cancellationToken);

        var paymentResponse = mapper.Map<PaymentResponse>(responseGrpc);

        // Para devolverlo igual a un reintento del mismo checkout (pendings/589).
        await orders.RecordInitiatedPaymentAsync(order.Id, new InitiatedPayment(paymentResponse.Success, (int)paymentResponse.NextAction, paymentResponse.RedirectUrl, paymentResponse.Metadata ?? []), cancellationToken);

        await pubsub.PublishAsync(order.GetAndClearEvents(), cancellationToken);

        return paymentResponse;
    }

    /// <summary>
    /// La respuesta del checkout a partir de la orden guardada: la del cobro que se inició o, si aún no se anotó, la de un
    /// cobro cuyo desenlace hay que esperar.
    /// </summary>
    private static PaymentResponse ResponseOf(OrderAggregate order) => order.InitiatedPayment is { } payment
        ? new PaymentResponse
        {
            PaymentId = order.PaymentId,
            Success = payment.Success,
            NextAction = (Order.DataTransferObjects.NextActionType)payment.NextAction,
            RedirectUrl = payment.RedirectUrl,
            Metadata = payment.Metadata,
        }
        : new PaymentResponse
        {
            PaymentId = order.PaymentId,
            Success = order.IsSuccess,
            NextAction = Order.DataTransferObjects.NextActionType.WaitConfirmation,
        };

    private async Task<InitiatePaymentResponse> PayLicenseAsync(OrderAggregate order, LicenseAggregate license, Currency tenantCurrency, CancellationToken cancellationToken)
    {
        var payRequest = mapper.Map<InitiatePaymentRequest>(order);

        payRequest.Id = order.PaymentId.ToString();
        payRequest.ReferenceId = order.Id.ToString();

        payRequest.Module = MODULE;
        payRequest.Provider = PaymentProvider.Payu;

        var isCorrectCurrency = order.License.Total.Currency == tenantCurrency.Code;
        ApplicationGuard.IsFalse(isCorrectCurrency, Errors.InvalidCurrency);

        var price = license.Prices.FirstOrDefault(x => 
            x.BillingType == order.License.BillingType && 
            x.BillingModel == order.License.BillingModel && 
            x.Total == order.License.Total);

        ApplicationGuard.IsNull(price, Errors.PriceInvalid);

        payRequest.SubTotal = new Amount
        {
            Currency = price.BasePrice.Currency, 
            Value = price.SubTotal.Amount
        };

        payRequest.Tax = new Amount
        {
            Currency = price.BasePrice.Currency,
            Value = price.Tax.Amount
        };

        payRequest.Total = new Amount
        {
            Currency = price.BasePrice.Currency,
            Value = price.Total.Amount
        };

        payRequest.Description = $"Payment for license {license.Name} to tenant {order.TenantDetail.Name}.";

        return await paymentGrpc.InitiatePaymentAsync(payRequest, cancellationToken);
    }
}