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
    IUserGrpc userGrpc,
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
            // Tenant does not exist, proceed with creation
        }


        var orderExists = await repository.ExistsAsync<OrderAggregate>(request.OrderDetail.Id, cancellationToken);

        ApplicationGuard.IsTrue(orderExists, Errors.OrderAlreadyExists);

        var payment = OrderAggregate.Create(request.OrderDetail.Id, request.Id, request.PaymentMethod, request.OrderDetail.Buyer, request.TenantDetail, user.IdUser);

        await ProcessPayment(request, cancellationToken);

        // var paymentResponse = await paymentGrpc.GetPayByIdAsync(new GetPaymentRequest { Id = request.OrderDetail.Id.ToString() }, cancellationToken);

        // payment.SetPaymentResponse(mapper.Map<Domain.ValueObjects.PaymentResponse>(paymentResponse));

        await repository.CreateAsync(payment, cancellationToken);

        await pubsub.PublishAsync(payment.GetAndClearEvents(), cancellationToken);
    }

    private async Task ProcessPayment(PayOrderCommand request, CancellationToken cancellationToken)
    {
        var license = await repository.FindAsync<LicenseAggregate>(request.Id, cancellationToken);

        await PayLicenseAsync(request, license.Prices, license, cancellationToken);

        // if (response.Response.Code == "SUCCESS" && response.Response.TransactionResponse.State == "APPROVED")
        // {
        //     await CreateTenantAsync(request.TenantDetail, license, cancellationToken);

        //     await UpdateUserAsync(request.TenantDetail.Name, request.TenantDetail.Id, cancellationToken);
        // }
    }

    private async Task PayLicenseAsync(PayOrderCommand request, List<Price> prices, LicenseAggregate license, CancellationToken cancellationToken)
    {
        var payRequest = mapper.Map<InitiatePaymentRequest>(request);

        payRequest.Module = MODULE;

        var price = prices
            .Where(x => x.BillingType == request.OrderDetail.BillingType && x.Total == request.OrderDetail.Total && x.BillingModel == request.OrderDetail.BillingModel)
            .FirstOrDefault();

        ApplicationGuard.IsNull(price, "202: The price for the selected billing type and model is not available.");

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

        await paymentGrpc.PayAsync(payRequest, cancellationToken);
    }

    private async Task CreateTenantAsync(Domain.ValueObjects.Tenant tenant, LicenseAggregate license, CancellationToken cancellationToken)
    {
        var tenantRequest = mapper.Map<CreateTenantRequest>(tenant);

        tenantRequest.License = new gRpc.Clients.Services.Tenant.License()
        {
            Id = license.Id.ToString(),
            Name = license.Name,
            StartDate = SystemClock.Instance.GetCurrentInstant().ToString(),
            EndDate = SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(license.Prices.FirstOrDefault()?.BillingType == BillingTypeEnum.Monthly ? 30 : 365)).ToString(),
        };

        foreach (var item in license.Attributes)
        {
            tenantRequest.License.Metadata.Add(item.Key, item.Value);
        }

        await tenantGrpc.CreateTenantAsync(tenantRequest, cancellationToken);
    }

    private async Task UpdateUserAsync(string nameTenant, Guid idTenant, CancellationToken cancellationToken)
    {
        await userGrpc.AddTenantToUser(new AddTenantRequest
        {
            Id = user.IdUser.ToString(),
            Tenant = new gRpc.Clients.Services.User.Tenant()
            {
                Id = idTenant.ToString(),
                Name = nameTenant
            }
        }, cancellationToken);

        await userGrpc.AddGroupToUser(new AddGroupRequest
        {
            Id = user.IdUser.ToString(),
            Role = "Administrator"
        }, cancellationToken);
    }

}