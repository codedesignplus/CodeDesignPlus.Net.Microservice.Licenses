using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions;

/// <summary>
/// Cuánto cuesta renovar la licencia con sus paquetes (pendings/735, 739): el mismo cálculo que la renovación.
/// </summary>
/// <param name="TenantId">La copropiedad del token.</param>
/// <param name="Packages">Los paquetes que se renuevan; nulo, los vigentes como están.</param>
public record GetRenewalQuoteQuery(Guid TenantId, List<PackageSelectionInput>? Packages) : IRequest<RenewalQuoteDto>;

/// <summary>
/// Cotiza la renovación.
/// </summary>
public class GetRenewalQuoteQueryHandler(IOrderRepository orders, ILicenseRepository licenses, IPackageRepository packages, ICreditBalanceRepository creditBalances) : IRequestHandler<GetRenewalQuoteQuery, RenewalQuoteDto>
{
    /// <inheritdoc/>
    public async Task<RenewalQuoteDto> Handle(GetRenewalQuoteQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);
        ApplicationGuard.GuidIsEmpty(request.TenantId, Errors.TenantIdIsRequired);

        var state = await TenantPackages.LoadAsync(orders, request.TenantId, cancellationToken);

        ApplicationGuard.IsNull(state, Errors.SubscriptionNotFound);

        var now = SystemClock.Instance.GetCurrentInstant();
        var current = state!.Subscription.CurrentAt(now);
        var catalog = await licenses.FindAsync<LicenseAggregate>(current.License.Id, cancellationToken);

        ApplicationGuard.IsTrue(catalog is null || catalog.IsDeleted, Errors.LicenseCannotBeRenewed);

        var price = catalog!.Prices.FirstOrDefault(x => x.BillingType == current.License.BillingType);

        ApplicationGuard.IsNull(price, Errors.LicenseCannotBeRenewed);

        var renewed = await state.RenewalQuotesAsync(packages, request.Packages, now, cancellationToken);
        var currency = price!.Total.Currency;
        var balance = await creditBalances.FindAsync<CreditBalanceAggregate>(request.TenantId, request.TenantId, cancellationToken);
        var awaiting = await orders.FindAwaitingSubscriptionPaymentAsync(request.TenantId, cancellationToken);
        var total = renewed.Aggregate(price.Total, (sum, x) => sum + x.Quote.Total);
        var charge = SubscriptionCharge.Compute(total, Money.Zero(currency), SubscriptionCheckout.AvailableCreditBalance(balance, awaiting, currency));

        return new RenewalQuoteDto
        {
            Currency = currency,
            BillingType = price.BillingType,
            License = new PackagePriceDto { SubTotal = price.SubTotal.Amount, Tax = price.Tax.Amount, Total = price.Total.Amount },
            Packages = [.. renewed.Select(x => PackageQuoteDto.From(x.Quote))],
            Total = total.Amount,
            CreditBalanceApplied = charge.CreditBalanceApplied.Amount,
            AmountDue = charge.AmountDue.Amount,
            RemainingCreditBalance = charge.RemainingCreditBalance.Amount
        };
    }
}

/// <summary>
/// Lo común de la renovación con paquetes.
/// </summary>
public static class RenewalQuote
{
    /// <summary>
    /// Si una renovación pendiente lleva los mismos paquetes con los mismos valores que los pedidos: entonces es el mismo
    /// cobro y se reutiliza; si no, la nueva la reemplaza.
    /// </summary>
    /// <param name="order">La renovación pendiente.</param>
    /// <param name="renewed">Los paquetes pedidos.</param>
    /// <returns><c>true</c> si son los mismos.</returns>
    public static bool SamePackages(OrderAggregate order, IReadOnlyCollection<(PackageQuote Quote, Guid? Replaces)> renewed)
    {
        var pending = (order.Packages ?? []).Select(x => Key(x.PackageId, x.Selections, x.Total.Amount)).OrderBy(x => x, StringComparer.Ordinal);
        var asked = renewed.Select(x => Key(x.Quote.Package.Id, x.Quote.Selections, x.Quote.Total.Amount)).OrderBy(x => x, StringComparer.Ordinal);

        return pending.SequenceEqual(asked, StringComparer.Ordinal);
    }

    private static string Key(Guid packageId, Dictionary<string, string> selections, long total) =>
        $"{packageId}|{total}|{string.Join(";", selections.OrderBy(x => x.Key, StringComparer.Ordinal).Select(x => $"{x.Key}={x.Value}"))}";
}
