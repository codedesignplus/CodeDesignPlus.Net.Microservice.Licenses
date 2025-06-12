using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.Payment;
using CodeDesignPlus.Net.gRpc.Clients.Services.Tenant;
using CodeDesignPlus.Net.gRpc.Clients.Services.User;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using DnsClient.Internal;
using Microsoft.Extensions.Logging;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.PayLicense;

public class PayLicenseCommandHandler(ILicenseRepository repository, IUserContext user, IPubSub pubsub, IMapper mapper, IPaymentGrpc paymentGrpc, IUserGrpc userGrpc, ITenantGrpc tenantGrpc, ILogger<PayLicenseCommandHandler> logger) : IRequestHandler<PayLicenseCommand>
{
    public async Task Handle(PayLicenseCommand request, CancellationToken cancellationToken)
    {
        logger.LogWarning("Processing PayLicenseCommand: {@Request}", request);
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var exist = await repository.ExistsAsync<LicenseAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsFalse(exist, Errors.LicenseNotFound);

        var aggregate = await repository.FindAsync<LicenseAggregate>(request.Id, cancellationToken);

        await PayLicenseAsync(request, aggregate.Prices, aggregate, cancellationToken);

        await CreateTenantAsync(request.Tenant, aggregate, cancellationToken);

        await UpdateUserAsync(request.Tenant.Name, request.Tenant.Id, cancellationToken);

        var license = PaymentAggregate.Create(request.Order.Id, request.Id, request.PaymentMethod, request.Order.Buyer, request.Tenant, user.Tenant, true, "Error", true, user.IdUser);

        await repository.CreateAsync(license, cancellationToken);

        await pubsub.PublishAsync(license.GetAndClearEvents(), cancellationToken);
    }

    private async Task PayLicenseAsync(PayLicenseCommand request, List<Price> prices, LicenseAggregate license, CancellationToken cancellationToken)
    {
        var payRequest = mapper.Map<PayRequest>(request);

        var price = prices
            .Where(x => x.BillingType == request.Order.BillingType && x.Total == request.Order.Total && x.BillingModel == request.Order.BillingModel)
            .FirstOrDefault();

        ApplicationGuard.IsNull(price, "202: The price for the selected billing type and model is not available.");

        payRequest.Transaction.Order.TaxReturnBase = new Amount
        {
            Currency = price.Currency.Code,
            Value = price.SubTotal
        };

        payRequest.Transaction.Order.Tax = new Amount
        {
            Currency = price.Currency.Code,
            Value = (long)price.Tax
        };

        payRequest.Transaction.Order.Amount = new Amount
        {
            Currency = price.Currency.Code,
            Value = price.Total
        };

        payRequest.Transaction.Order.Description = $"Payment for license {license.Name} to tenant {request.Tenant.Name}. Order ID: {request.Order.Id}";

        await paymentGrpc.PayAsync(payRequest, cancellationToken);

        var paymentResponse = await paymentGrpc.GetPayByIdAsync(new GetPaymentRequest { Id = request.Order.Id.ToString() }, cancellationToken);

        logger.LogWarning("Payment response: {@PaymentResponse}", paymentResponse);
    }

    private async Task CreateTenantAsync(Domain.ValueObjects.Tenant tenant, LicenseAggregate license, CancellationToken cancellationToken)
    {
        logger.LogWarning("Creating tenant for license: {@Tenant}", tenant);
        logger.LogWarning("License details: {@License}", license);

        var tenantRequest = mapper.Map<CreateTenantRequest>(tenant);

        logger.LogWarning("Tenant request: {@TenantRequest}", tenantRequest);

        tenantRequest.License = new gRpc.Clients.Services.Tenant.License()
        {
            Id = license.Id.ToString(),
            Name = license.Name,
            StartDate = SystemClock.Instance.GetCurrentInstant().ToString(),
            EndDate = SystemClock.Instance.GetCurrentInstant().Plus(Duration.FromDays(license.Prices.FirstOrDefault()?.BillingType == BillingTypeEnum.Monthly ? 30 : 365)).ToString(),
        };

        logger.LogWarning("Tenant request with license: {@TenantRequestWithLicense}", tenantRequest);

        foreach (var item in license.Attributes)
        {
            tenantRequest.License.Metadata.Add(item.Key, item.Value);
        }

        logger.LogWarning("Tenant request with metadata: {@TenantRequestWithMetadata}", tenantRequest);

        await tenantGrpc.CreateTenantAsync(tenantRequest, cancellationToken);

        logger.LogWarning("Tenant created successfully: {@TenantRequest}", tenantRequest);
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