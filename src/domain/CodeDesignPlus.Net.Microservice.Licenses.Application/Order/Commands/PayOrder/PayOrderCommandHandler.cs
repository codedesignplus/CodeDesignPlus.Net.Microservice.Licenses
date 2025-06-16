using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.Payment;
using CodeDesignPlus.Net.gRpc.Clients.Services.Tenant;
using CodeDesignPlus.Net.gRpc.Clients.Services.User;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;

public class PayOrderCommandHandler(
    ILicenseRepository repository,
    IUserContext user,
    IPubSub pubsub,
    IMapper mapper,
    IPaymentGrpc paymentGrpc,
    IUserGrpc userGrpc,
    ITenantGrpc tenantGrpc,
    ILogger<PayOrderCommandHandler> logger
) : IRequestHandler<PayOrderCommand>
{
    private const string MODULE = "Licenses";

    public async Task Handle(PayOrderCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var exist = await repository.ExistsAsync<LicenseAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsFalse(exist, Errors.LicenseNotFound);

        var payment = OrderAggregate.Create(request.Order.Id, request.Id, request.PaymentMethod, request.Order.Buyer, request.Tenant, user.Tenant, true, user.IdUser);

        await ProcessPayment(request, cancellationToken);

        var paymentResponse = await paymentGrpc.GetPayByIdAsync(new GetPaymentRequest { Id = request.Order.Id.ToString() }, cancellationToken);

        payment.SetPaymentResponse(mapper.Map<Domain.ValueObjects.PaymentResponse>(paymentResponse));

        await repository.CreateAsync(payment, cancellationToken);

        await pubsub.PublishAsync(payment.GetAndClearEvents(), cancellationToken);
    }

    private async Task ProcessPayment(PayOrderCommand request, CancellationToken cancellationToken)
    {
        var license = await repository.FindAsync<LicenseAggregate>(request.Id, cancellationToken);

        await PayLicenseAsync(request, license.Prices, license, cancellationToken);

        await CreateTenantAsync(request.Tenant, license, cancellationToken);

        await UpdateUserAsync(request.Tenant.Name, request.Tenant.Id, cancellationToken);
    }

    private async Task PayLicenseAsync(PayOrderCommand request, List<Price> prices, LicenseAggregate license, CancellationToken cancellationToken)
    {
        var payRequest = mapper.Map<PayRequest>(request);

        payRequest.Module = MODULE;

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