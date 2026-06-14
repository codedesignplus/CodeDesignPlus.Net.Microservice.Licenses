using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetSubscription;

/// <summary>
/// Handler para obtener la suscripción activa de un tenant.
/// Busca la orden exitosa para el tenant y la licencia indicada, retornando el snapshot completo.
/// Money amounts are converted from minor units to decimals using ICurrencyGrpc.
/// </summary>
public class GetSubscriptionQueryHandler(IOrderRepository repository, ICurrencyGrpc currencyGrpc)
    : IRequestHandler<GetSubscriptionQuery, SubscriptionDto>
{
    public async Task<SubscriptionDto> Handle(GetSubscriptionQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);
        ApplicationGuard.GuidIsEmpty(request.TenantId, Errors.TenantIdIsRequired);
        ApplicationGuard.GuidIsEmpty(request.LicenseId, Errors.LicenseIdIsRequired);

        var order = await repository.FindActiveSubscriptionAsync(request.TenantId, request.LicenseId, cancellationToken);

        ApplicationGuard.IsNull(order, Errors.OrderNotFound);

        var currency = await currencyGrpc.GetCurrencyAsync(code: order.License.Total.Currency, cancellationToken: cancellationToken);
        var decimalDigits = currency.DecimalDigits;

        return new SubscriptionDto
        {
            Id = order.Id,
            PaymentStatus = order.PaymentStatus,
            ProvisioningStatus = order.ProvisioningStatus,
            CreatedAt = order.CreatedAt,
            IsActive = order.IsActive,
            License = new SubscriptionLicenseDto
            {
                Id = order.License.Id,
                Name = order.License.Name,
                Total = order.License.Total.ToDecimal(decimalDigits),
                Tax = order.License.Tax.ToDecimal(decimalDigits),
                SubTotal = order.License.SubTotal.ToDecimal(decimalDigits),
                Currency = order.License.Total.Currency,
                BillingType = order.License.BillingType,
                BillingModel = order.License.BillingModel,
                Description = order.License.Description,
                ShortDescription = order.License.ShortDescription,
                Icon = order.License.Icon,
                TermsOfService = order.License.TermsOfService,
                IsPopular = order.License.IsPopular,
                ShowInLandingPage = order.License.ShowInLandingPage,
                Attributes = order.License.Attributes,
                Modules = order.License.Modules
            }
        };
    }
}
