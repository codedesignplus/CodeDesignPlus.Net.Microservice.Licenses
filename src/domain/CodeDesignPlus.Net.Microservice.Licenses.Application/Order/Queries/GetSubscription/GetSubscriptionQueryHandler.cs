using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;
using Microsoft.Extensions.Options;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetSubscription;

/// <summary>
/// Arma la suscripción de una copropiedad con sus órdenes pagadas: la licencia en curso, hasta cuándo está pagada, su
/// estado y su saldo a favor (pendings/735).
/// </summary>
/// <remarks>
/// Money amounts are returned in minor units; the client converts for display.
/// </remarks>
public class GetSubscriptionQueryHandler(IOrderRepository repository, ICreditBalanceRepository creditBalances, IOptions<SubscriptionOptions> options)
    : IRequestHandler<GetSubscriptionQuery, SubscriptionDto>
{
    public async Task<SubscriptionDto> Handle(GetSubscriptionQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);
        ApplicationGuard.GuidIsEmpty(request.TenantId, Errors.TenantIdIsRequired);

        var state = await Subscriptions.TenantPackages.LoadAsync(repository, request.TenantId, cancellationToken);

        ApplicationGuard.IsNull(state, Errors.OrderNotFound);

        var subscription = state!.Subscription;

        var now = SystemClock.Instance.GetCurrentInstant();
        var order = subscription.CurrentAt(now);
        var balance = await creditBalances.FindAsync<CreditBalanceAggregate>(request.TenantId, request.TenantId, cancellationToken);
        var awaiting = await repository.FindAwaitingSubscriptionPaymentAsync(request.TenantId, cancellationToken);
        var paidUp = state.IsPaidUp(now);
        var current = state.Current(now).Select(x => PackageLineDto.From(x.Line, x.Order.Id, paidUp));
        var upcoming = PackageTerm.Upcoming(state.Lines, now).Select(x => PackageLineDto.From(x.Line, x.Order.Id, false));

        return new SubscriptionDto
        {
            Id = order.Id,
            PaymentStatus = order.PaymentStatus,
            ProvisioningStatus = order.ProvisioningStatus,
            CreatedAt = order.CreatedAt,
            IsActive = order.IsActive,
            PeriodStart = order.PeriodStart!.Value,
            PeriodEnd = subscription.PaidThrough,
            DaysRemaining = subscription.DaysRemaining(now),
            Status = subscription.StatusAt(now, options.Value.ExpiringWindowDays, options.Value.GraceDays),
            GraceEndsAt = subscription.GraceEndsAt(options.Value.GraceDays),
            CreditBalance = balance?.Balance.Amount ?? 0,
            Currency = order.License.Total.Currency,
            BillingType = order.License.BillingType,
            HasPaymentInProgress = awaiting.Count > 0,
            Packages = [.. current.Concat(upcoming)],
            EffectiveAttributes = state.EffectiveAttributes(now),
            License = new SubscriptionLicenseDto
            {
                Id = order.License.Id,
                Name = order.License.Name,
                Total = order.License.Total.Amount,
                Tax = order.License.Tax.Amount,
                SubTotal = order.License.SubTotal.Amount,
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
