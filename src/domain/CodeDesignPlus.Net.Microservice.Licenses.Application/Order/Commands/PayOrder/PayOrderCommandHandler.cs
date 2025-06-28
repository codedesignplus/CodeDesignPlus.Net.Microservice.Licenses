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
    private const string MODULE = "Licenses";

    public async Task<PaymentResponse> Handle(PayOrderCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var existLicense = await repository.ExistsAsync<LicenseAggregate>(request.License.Id, cancellationToken);
        ApplicationGuard.IsFalse(existLicense, Errors.LicenseNotFound);

        try
        {
            var existTenant = await tenantGrpc.GetTenantByIdAsync(new GetTenantRequest { Id = request.TenantDetail.Id.ToString() }, cancellationToken);
            ApplicationGuard.IsNotNull(existTenant, Errors.TenantAlreadyExists);
        }
        catch (RpcException ex) when (ex.StatusCode == StatusCode.FailedPrecondition)
        {
            // Tenant does not exist, proceed with payment
        }

        var orderExists = await repository.ExistsAsync<OrderAggregate>(request.Id, cancellationToken);
        ApplicationGuard.IsTrue(orderExists, Errors.OrderAlreadyExists);

        var license = await repository.FindAsync<LicenseAggregate>(request.License.Id, cancellationToken);
        var response = await PayLicenseAsync(request, license.Prices, license, cancellationToken);

        logger.LogWarning("Pay license {LicenseName} for tenant {TenantName} with response {@Response}", license.Name, request.TenantDetail.Name, response);

        var payment = OrderAggregate.Create(request.Id, request.License, request.PaymentMethod, request.Buyer, request.TenantDetail, user.IdUser);

        var paymentResponse = mapper.Map<PaymentResponse>(response);

        if (request.PaymentMethod.Code != "PSE")
            payment.SetPaymentResponse(paymentResponse);

        await repository.CreateAsync(payment, cancellationToken);

        await pubsub.PublishAsync(payment.GetAndClearEvents(), cancellationToken);

        return paymentResponse;
    }

    private async Task<InitiatePaymentRequest> PayLicenseAsync(PayOrderCommand request, List<Price> prices, LicenseAggregate license, CancellationToken cancellationToken)
    {
        var payRequest = mapper.Map<InitiatePaymentRequest>(request);

        payRequest.Module = MODULE;
        payRequest.Provider = PaymentProvider.Payu;

        var price = prices
            .Where(x => x.BillingType == request.License.BillingType && x.Total == request.License.Total && x.BillingModel == request.License.BillingModel)
            .FirstOrDefault();

        logger.LogWarning("Paying license {LicenseName} for tenant {TenantName} with price {@Price}", license.Name, request.TenantDetail.Name, price);

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

        payRequest.Description = $"Payment for license {license.Name} to tenant {request.TenantDetail.Name}.";

        await paymentGrpc.InitiatePaymentAsync(payRequest, cancellationToken);

        return payRequest;
    }

}