using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.Payment;
using CodeDesignPlus.Net.gRpc.Clients.Services.Tenant;
using CodeDesignPlus.Net.gRpc.Clients.Services.User;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;

public class UpdateStateOrderCommandHandler(IOrderRepository orderRepository,
    ILicenseRepository licenseRepository,
    IUserContext user,
    IPubSub pubsub,
    IMapper mapper,
    IPaymentGrpc paymentGrpc,
    IUserGrpc userGrpc,
    ITenantGrpc tenantGrpc) : IRequestHandler<UpdateStateOrderCommand, PaymentResponse>
{
    public async Task<PaymentResponse> Handle(UpdateStateOrderCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var order = await orderRepository.FindAsync<OrderAggregate>(request.Id, cancellationToken);
        ApplicationGuard.IsNull(order, Errors.OrderNotFound);

        var license = await licenseRepository.FindAsync<LicenseAggregate>(order.License.Id, cancellationToken);
        ApplicationGuard.IsNull(license, Errors.LicenseNotFound);

        if (order.PaymentResponse is not null)
            return order.PaymentResponse;

        var statusPayment = await paymentGrpc.UpdateStatusAsync(request.Id, cancellationToken);

        var response = mapper.Map<PaymentResponse>(statusPayment);

        if (statusPayment.Status == gRpc.Clients.Services.Payment.PaymentStatus.Succeeded)
        {
            await this.CreateTenantAsync(order.TenantDetail, license, cancellationToken);

            await this.UpdateUserAsync(order.TenantDetail.Name, order.TenantDetail.Id, cancellationToken);
        }

        order.SetPaymentResponse(response);

        await orderRepository.UpdateAsync(order, cancellationToken);

        await pubsub.PublishAsync(order.GetAndClearEvents(), cancellationToken);

        return response;
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