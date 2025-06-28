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
    ITenantGrpc tenantGrpc
) : IRequestHandler<PayOrderCommand>
{
    private const string MODULE = "Licenses";

    public async Task Handle(PayOrderCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var existLicense = await repository.ExistsAsync<LicenseAggregate>(request.Id, cancellationToken);
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

        var orderExists = await repository.ExistsAsync<OrderAggregate>(request.OrderDetail.Id, cancellationToken);
        ApplicationGuard.IsTrue(orderExists, Errors.OrderAlreadyExists);

        var license = await repository.FindAsync<LicenseAggregate>(request.Id, cancellationToken);
        await PayLicenseAsync(request, license.Prices, license, cancellationToken);

        var payment = OrderAggregate.Create(request.OrderDetail.Id, request.Id, request.PaymentMethod, request.OrderDetail.Buyer, request.TenantDetail, user.IdUser);

        await repository.CreateAsync(payment, cancellationToken);

        await pubsub.PublishAsync(payment.GetAndClearEvents(), cancellationToken);
    }

    private async Task PayLicenseAsync(PayOrderCommand request, List<Price> prices, LicenseAggregate license, CancellationToken cancellationToken)
    {
        var payRequest = mapper.Map<InitiatePaymentRequest>(request);

        payRequest.Module = MODULE;

        var price = prices
            .Where(x => x.BillingType == request.OrderDetail.BillingType && x.Total == request.OrderDetail.Total && x.BillingModel == request.OrderDetail.BillingModel)
            .FirstOrDefault();

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
    }

}