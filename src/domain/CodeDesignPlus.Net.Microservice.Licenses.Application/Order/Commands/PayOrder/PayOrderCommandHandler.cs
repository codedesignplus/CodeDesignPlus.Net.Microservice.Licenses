using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.Payment;
using CodeDesignPlus.Net.gRpc.Clients.Services.Tenant;
using CodeDesignPlus.Net.gRpc.Clients.Services.User;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Grpc.Core;
using Microsoft.Extensions.Logging;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;

public class PayOrderCommandHandler(
    ILicenseRepository repository,
    IUserContext user,
    IPubSub pubsub,
    IMapper mapper,
    IPaymentGrpc paymentGrpc,
    ITenantGrpc tenantGrpc,
    ILogger<PayOrderCommandHandler> logger
) : IRequestHandler<PayOrderCommand, PaymentResponse>
{
    public const string MODULE = "Licenses";

    public async Task<PaymentResponse> Handle(PayOrderCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var existLicense = await repository.ExistsAsync<LicenseAggregate>(request.License.Id, cancellationToken);
        ApplicationGuard.IsFalse(existLicense, Errors.LicenseNotFound);

        var existTenant = await tenantGrpc.ExistTenantAsync(request.TenantDetail.Id, cancellationToken);
        ApplicationGuard.IsNotNull(existTenant, Errors.TenantAlreadyExists);

        var orderExists = await repository.ExistsAsync<OrderAggregate>(request.Id, cancellationToken);
        ApplicationGuard.IsTrue(orderExists, Errors.OrderAlreadyExists);

        var license = await repository.FindAsync<LicenseAggregate>(request.License.Id, cancellationToken);
        ApplicationGuard.IsNull(license, Errors.LicenseNotFound);

        var payment = OrderAggregate.Create(request.Id, Guid.NewGuid(), request.License, request.PaymentMethod, request.Buyer, request.TenantDetail, user.IdUser);

        var responseGrpc = await PayLicenseAsync(payment, license.Prices, license, cancellationToken);

        var paymentResponse = mapper.Map<PaymentResponse>(responseGrpc);

        await repository.CreateAsync(payment, cancellationToken);

        await pubsub.PublishAsync(payment.GetAndClearEvents(), cancellationToken);

        return paymentResponse;
    }

    private async Task<InitiatePaymentResponse> PayLicenseAsync(OrderAggregate payment, List<Price> prices, LicenseAggregate license, CancellationToken cancellationToken)
    {
        var payRequest = mapper.Map<InitiatePaymentRequest>(payment);

        payRequest.Module = MODULE;
        payRequest.Provider = PaymentProvider.Payu;

        var price = prices
            .Where(x => x.BillingType == payment.License.BillingType && x.Total == payment.License.Total && x.BillingModel == payment.License.BillingModel)
            .FirstOrDefault();

        logger.LogWarning("Paying license {LicenseName} for tenant {TenantName} with price {@Price}", license.Name, payment.TenantDetail.Name, price);

        ApplicationGuard.IsNull(price!, "202: The price for the selected billing type and model is not available.");

        payRequest.SubTotal = new Amount
        {
            Currency = price.Currency.Code,
            Value = price.SubTotal
        };

        payRequest.Tax = new Amount
        {
            Currency = price.Currency.Code,
            Value = (long)price.Tax
        };

        payRequest.Total = new Amount
        {
            Currency = price.Currency.Code,
            Value = price.Total
        };

        payRequest.Description = $"Payment for license {license.Name} to tenant {payment.TenantDetail.Name}.";

        return await paymentGrpc.InitiatePaymentAsync(payRequest, cancellationToken);
    }

}