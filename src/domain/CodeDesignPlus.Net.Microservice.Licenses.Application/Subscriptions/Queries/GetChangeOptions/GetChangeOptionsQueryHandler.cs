using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Queries.GetChangeOptions;

/// <summary>
/// Lista las licencias mayores que la de la copropiedad con su cotización (pendings/738).
/// </summary>
/// <remarks>
/// Solo ofrece lo que el cambio acepta (regla 42): licencias que se le ofrecen a la copropiedad, con precio para su
/// periodicidad y con todos sus módulos y atributos iguales o mayores. Las más baratas primero.
/// </remarks>
public class GetChangeOptionsQueryHandler(IOrderRepository orders, ILicenseRepository licenses, ICreditBalanceRepository creditBalances) : IRequestHandler<GetChangeOptionsQuery, List<ChangeOptionDto>>
{
    public async Task<List<ChangeOptionDto>> Handle(GetChangeOptionsQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);
        ApplicationGuard.GuidIsEmpty(request.TenantId, Errors.TenantIdIsRequired);

        var subscription = SubscriptionTimeline.From(await orders.FindPaidByTenantAsync(request.TenantId, cancellationToken));

        ApplicationGuard.IsNull(subscription, Errors.SubscriptionNotFound);

        var now = SystemClock.Instance.GetCurrentInstant();
        var currency = subscription.CurrentAt(now).License.Total.Currency;
        var balance = await creditBalances.FindAsync<CreditBalanceAggregate>(request.TenantId, request.TenantId, cancellationToken);
        var awaiting = await orders.FindAwaitingSubscriptionPaymentAsync(request.TenantId, cancellationToken);
        var available = SubscriptionCheckout.AvailableCreditBalance(balance, awaiting, currency);

        var catalog = await licenses.FindOnSaleAsync(cancellationToken);

        return [.. catalog
            .Select(x => SubscriptionChangeQuote.For(subscription, x, available, now))
            .Where(x => x is not null)
            .Select(x => ToDto(x!))
            .OrderBy(x => x.Price)
            .ThenBy(x => x.Name)];
    }

    /// <summary>
    /// La opción de cambio a partir de su cotización.
    /// </summary>
    /// <param name="quote">La cotización.</param>
    /// <returns>La opción.</returns>
    public static ChangeOptionDto ToDto(SubscriptionChangeQuote quote) => new()
    {
        LicenseId = quote.Target.Id,
        Name = quote.Target.Name,
        ShortDescription = quote.Target.ShortDescription,
        Description = quote.Target.Description,
        Icon = quote.Target.Icon,
        IsPopular = quote.Target.IsPopular,
        BillingType = quote.Price.BillingType,
        Currency = quote.Price.Total.Currency,
        Price = quote.Price.Total.Amount,
        PriceSubTotal = quote.Price.SubTotal.Amount,
        PriceTax = quote.Price.Tax.Amount,
        TaxTreatment = quote.Price.TaxTreatment,
        TaxBasisPoints = quote.Price.TaxBasisPoints,
        TaxLegalBasis = quote.Price.TaxLegalBasis,
        Credit = quote.Charge.ProrationCredit.Amount,
        CreditApplied = quote.Charge.CreditApplied.Amount,
        CreditBalanceApplied = quote.Charge.CreditBalanceApplied.Amount,
        Total = quote.Charge.AmountDue.Amount,
        RemainingCreditBalance = quote.Charge.RemainingCreditBalance.Amount,
        CreditDetail = [.. quote.CreditedPeriods.Select(x => new CreditedPeriodDto
        {
            OrderId = x.OrderId,
            From = x.From,
            To = x.To,
            UnusedDays = x.UnusedDays,
            PeriodDays = x.PeriodDays,
            Base = x.Base.Amount,
            Amount = x.Amount.Amount,
        })],
        GainedModules = [.. quote.Upgrade.GainedModules.Select(ToModule)],
        GainedAttributes = new Dictionary<string, string>(quote.Upgrade.GainedAttributes),
        Modules = [.. quote.Target.Modules.Select(ToModule)],
        Attributes = new Dictionary<string, string>(quote.Target.Attributes),
    };

    private static ModuleDto ToModule(ModuleEntity module) => new() { Id = module.Id, Name = module.Name, Description = module.Description };
}
