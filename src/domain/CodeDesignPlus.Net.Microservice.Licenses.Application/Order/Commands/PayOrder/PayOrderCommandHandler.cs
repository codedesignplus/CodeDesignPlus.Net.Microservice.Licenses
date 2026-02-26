using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.Payment;
using CodeDesignPlus.Net.ValueObjects.Financial;
using CodeDesignPlus.Net.ValueObjects.User;
using Microsoft.Extensions.Logging;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;

public class PayOrderCommandHandler(
    ILicenseRepository repository,
    IUserContext user,
    IPubSub pubsub,
    IMapper mapper,
    IPaymentGrpc paymentGrpc,
    ITenantGrpc tenantGrpc
) : IRequestHandler<PayOrderCommand, PaymentResponse>
{
    public const string MODULE = "Licenses";

    public async Task<PaymentResponse> Handle(PayOrderCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var existLicense = await repository.ExistsAsync<LicenseAggregate>(request.License.Id, cancellationToken);
        ApplicationGuard.IsFalse(existLicense, Errors.LicenseNotFound);

        var existTenant = await tenantGrpc.ExistTenantAsync(request.TenantDetail.Id, cancellationToken);
        ApplicationGuard.IsTrue(existTenant, Errors.TenantAlreadyExists);

        var orderExists = await repository.ExistsAsync<OrderAggregate>(request.Id, cancellationToken);
        ApplicationGuard.IsTrue(orderExists, Errors.OrderAlreadyExists);

        var license = await repository.FindAsync<LicenseAggregate>(request.License.Id, cancellationToken);
        ApplicationGuard.IsNull(license, Errors.LicenseNotFound);

        var buyer = Buyer.Create(user.IdUser, request.Buyer.Name, request.Buyer.Phone, request.Buyer.Email, request.Buyer.TypeDocument, request.Buyer.Document);

        var order = OrderAggregate.Create(request.Id, Guid.NewGuid(), request.License, request.PaymentMethod, buyer, request.TenantDetail, user.IdUser);
        
        //buyer.SetBuyerId(user.IdUser);

        var payment = OrderAggregate.Create(request.Id, Guid.NewGuid(), request.License, request.PaymentMethod, buyer, request.TenantDetail, user.IdUser);

        var responseGrpc = await PayLicenseAsync(order, license, request.TenantDetail.Location.Country.Currency, cancellationToken);

        var paymentResponse = mapper.Map<PaymentResponse>(responseGrpc);

        await repository.CreateAsync(order, cancellationToken);

        await pubsub.PublishAsync(order.GetAndClearEvents(), cancellationToken);

        return paymentResponse;
    }

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
            Value = price.SubTotal.ToLong(tenantCurrency.DecimalDigits)
        };

        payRequest.Tax = new Amount
        {
            Currency = price.BasePrice.Currency,
            Value = price.Tax.ToLong(tenantCurrency.DecimalDigits)
        };

        payRequest.Total = new Amount
        {
            Currency = price.BasePrice.Currency,
            Value = price.Total.ToLong(tenantCurrency.DecimalDigits)
        };

        payRequest.Description = $"Payment for license {license.Name} to tenant {order.TenantDetail.Name}.";

        return await paymentGrpc.InitiatePaymentAsync(payRequest, cancellationToken);
    }
}