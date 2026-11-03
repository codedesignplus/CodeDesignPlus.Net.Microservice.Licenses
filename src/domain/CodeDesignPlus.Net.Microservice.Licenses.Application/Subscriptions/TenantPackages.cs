using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;
using LicenseSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.License;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions;

/// <summary>
/// La suscripción de una copropiedad con sus paquetes: la licencia en curso y las líneas de paquete pagadas (pendings/739).
/// </summary>
/// <param name="Subscription">La licencia en línea de tiempo.</param>
/// <param name="Lines">Las líneas de paquete pagadas, de cualquier orden.</param>
public sealed record TenantPackages(SubscriptionTimeline Subscription, List<(OrderAggregate Order, PackageLine Line)> Lines)
{
    /// <summary>
    /// Arma la suscripción de la copropiedad.
    /// </summary>
    /// <param name="orders">El repositorio de órdenes.</param>
    /// <param name="tenant">La copropiedad.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>La suscripción, o <c>null</c> si no tiene ninguna orden pagada.</returns>
    public static async Task<TenantPackages?> LoadAsync(IOrderRepository orders, Guid tenant, CancellationToken cancellationToken)
    {
        var subscription = SubscriptionTimeline.From(await orders.FindPaidByTenantAsync(tenant, cancellationToken));

        if (subscription is null)
        {
            return null;
        }

        return new TenantPackages(subscription, PackageTerm.PaidLines(await orders.FindPaidWithPackagesByTenantAsync(tenant, cancellationToken) ?? []));
    }

    /// <summary>
    /// Si la licencia está pagada en ese instante: los paquetes solo cuentan, y solo se compran, con la licencia al día.
    /// </summary>
    /// <param name="now">El instante.</param>
    /// <returns><c>true</c> si está pagada.</returns>
    public bool IsPaidUp(Instant now) => Subscription.Orders.Any(x => x.PeriodStart!.Value <= now && now < x.PeriodEnd!.Value);

    /// <summary>
    /// Las líneas vigentes (sin mirar la licencia).
    /// </summary>
    /// <param name="now">El instante.</param>
    /// <returns>Las líneas.</returns>
    public List<(OrderAggregate Order, PackageLine Line)> Current(Instant now) => PackageTerm.Current(Lines, now);

    /// <summary>
    /// La última línea de cada paquete entre las vigentes y las ya pagadas que empiezan después: la que renueva la
    /// renovación siguiente.
    /// </summary>
    /// <param name="now">El instante.</param>
    /// <returns>Una línea por paquete.</returns>
    public List<(OrderAggregate Order, PackageLine Line)> Latest(Instant now) => [.. Current(now)
        .Concat(PackageTerm.Upcoming(Lines, now))
        .GroupBy(x => x.Line.PackageId)
        .Select(g => g.OrderBy(x => x.Line.PeriodStart).Last())];

    /// <summary>
    /// Los atributos efectivos en ese instante.
    /// </summary>
    /// <param name="now">El instante.</param>
    /// <returns>Los atributos de la licencia más los de los paquetes que cuentan.</returns>
    public Dictionary<string, string> EffectiveAttributes(Instant now) => PackageTerm.EffectiveAttributes(Subscription.CurrentAt(now).License.Attributes, IsPaidUp(now) ? Subscription.PaidThrough : now, Lines, now);

    /// <summary>
    /// La copia de la licencia en curso con precio cero, para una orden de paquete que no la cobra.
    /// </summary>
    /// <param name="now">El instante.</param>
    /// <returns>La copia.</returns>
    public LicenseSnapshot ReferenceLicense(Instant now)
    {
        var current = Subscription.CurrentAt(now).License;
        var zero = Money.Zero(current.Total.Currency);

        return LicenseSnapshot.Create(current.Id, current.Name, zero, zero, zero, current.BillingType, current.BillingModel, current.Description, current.ShortDescription, current.Icon, current.TermsOfService, current.IsPopular, current.ShowInLandingPage, new Dictionary<string, string>(current.Attributes), [.. current.Modules]);
    }

    /// <summary>
    /// Cotiza la compra o la ampliación de un paquete (pendings/739).
    /// </summary>
    /// <param name="package">El paquete del catálogo.</param>
    /// <param name="selections">Lo elegido.</param>
    /// <param name="creditBalance">El saldo a favor disponible.</param>
    /// <param name="now">El instante.</param>
    /// <returns>La cotización con el crédito, el cobro y el periodo.</returns>
    public PackageAddOnQuote QuoteAddOn(PackageAggregate package, IReadOnlyDictionary<string, string>? selections, Money creditBalance, Instant now)
    {
        ApplicationGuard.IsFalse(IsPaidUp(now), Errors.LicenseIsNotPaidUp);
        ApplicationGuard.IsTrue(package.IsDeleted || !package.IsOfferedTo(Subscription.TenantId), Errors.PackageIsNotOffered);

        var current = Subscription.CurrentAt(now);

        ApplicationGuard.IsTrue(package.Currency != current.License.Total.Currency, Errors.PackageCurrencyDoesNotMatch);

        var quote = PackagePricing.Quote(package, selections);
        var existing = Current(now).Where(x => x.Line.PackageId == package.Id).Cast<(OrderAggregate Order, PackageLine Line)?>().FirstOrDefault();
        var credited = new List<CreditedPeriod>();

        if (existing is not null)
        {
            // Ampliar, nunca reducir a mitad de periodo: reducir o quitar se hace al renovar (decisión del usuario).
            ApplicationGuard.IsFalse(AttributeCombiner.IsUpgrade(existing.Value.Line.Attributes, quote.Attributes), Errors.PackageIsNotAnUpgrade);

            var credit = Proration.CreditLine(existing.Value.Order.Id, existing.Value.Line, Subscription.Zone, now);

            if (credit is not null)
            {
                credited.Add(credit);
            }
        }

        var creditTotal = credited.Aggregate(Money.Zero(quote.Total.Currency), (sum, x) => sum + x.Total);
        var charge = SubscriptionCharge.Compute(quote.Total, creditTotal, creditBalance);
        var end = PackageTerm.End(now, current.License.BillingType, current.PeriodEnd);

        return new PackageAddOnQuote(quote, existing?.Line.LineId, credited, charge, now, end);
    }

    /// <summary>
    /// Las líneas que renueva una renovación de la licencia (pendings/739): con licencia anual, las pedidas o, si no se pide
    /// nada, la última de cada paquete con los mismos valores al precio vigente. Con licencia mensual, ninguna.
    /// </summary>
    /// <param name="packages">El catálogo.</param>
    /// <param name="requested">Lo pedido; nulo para renovar los vigentes como están.</param>
    /// <param name="now">El instante.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Las cotizaciones con la línea que reemplaza cada una.</returns>
    public async Task<List<(PackageQuote Quote, Guid? Replaces)>> RenewalQuotesAsync(IPackageRepository packages, IReadOnlyCollection<PackageSelectionInput>? requested, Instant now, CancellationToken cancellationToken)
    {
        var license = Subscription.CurrentAt(now).License;

        if (!PackageTerm.IsCoterminous(license.BillingType))
        {
            ApplicationGuard.IsTrue(requested is { Count: > 0 }, Errors.PackagesRenewOnlyWithAnnualLicense);

            return [];
        }

        var latest = Latest(now).ToDictionary(x => x.Line.PackageId, x => x.Line);

        if (requested is null)
        {
            var renewed = new List<(PackageQuote, Guid?)>();

            foreach (var line in latest.Values)
            {
                var package = await packages.FindAsync<PackageAggregate>(line.PackageId, cancellationToken);

                // Un paquete borrado ya no se renueva; uno retirado de la venta sí, como la licencia (pendings/735).
                if (package is null || package.IsDeleted)
                {
                    continue;
                }

                renewed.Add((PackagePricing.Quote(package, line.Selections), line.LineId));
            }

            return renewed;
        }

        var tenant = Subscription.TenantId;
        var quotes = await Package.Services.PackageSelections.QuoteAsync(packages, requested, license.Total.Currency, x => x.IsOfferedTo(tenant) || latest.ContainsKey(x.Id), Errors.PackageIsNotOffered, cancellationToken);

        return [.. quotes.Select(x => (x, latest.TryGetValue(x.Package.Id, out var line) ? line.LineId : (Guid?)null))];
    }

    /// <summary>
    /// Las líneas que recotiza un cambio de licencia (pendings/739): con licencia anual, las vigentes con los mismos valores
    /// al precio vigente, con crédito por lo no usado de cada una; con licencia mensual los paquetes siguen como están.
    /// </summary>
    /// <param name="packages">El catálogo.</param>
    /// <param name="now">El instante.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Las líneas nuevas y lo acreditado de las anteriores.</returns>
    public async Task<(List<PackageQuote> Quotes, List<PackageLine> Lines, List<CreditedPeriod> Credited)> ChangeLinesAsync(IPackageRepository packages, Instant now, CancellationToken cancellationToken)
    {
        var quotes = new List<PackageQuote>();
        var lines = new List<PackageLine>();
        var credited = new List<CreditedPeriod>();

        if (!PackageTerm.IsCoterminous(Subscription.CurrentAt(now).License.BillingType))
        {
            return (quotes, lines, credited);
        }

        foreach (var (order, line) in Current(now))
        {
            var package = await packages.FindAsync<PackageAggregate>(line.PackageId, cancellationToken);

            if (package is null || package.IsDeleted)
            {
                // Ya no se vende: sigue hasta su fin sin recotizarse.
                continue;
            }

            var quote = PackagePricing.Quote(package, line.Selections);
            var credit = Proration.CreditLine(order.Id, line, Subscription.Zone, now);

            quotes.Add(quote);
            lines.Add(quote.ToLine(Guid.NewGuid(), line.LineId));

            if (credit is not null)
            {
                credited.Add(credit);
            }
        }

        return (quotes, lines, credited);
    }
}

/// <summary>
/// La cotización de la compra o la ampliación de un paquete para una copropiedad (pendings/739).
/// </summary>
/// <param name="Quote">El precio del paquete.</param>
/// <param name="ReplacesLineId">La línea que amplía; nula en una compra.</param>
/// <param name="CreditedPeriods">Lo no usado de esa línea.</param>
/// <param name="Charge">El cobro con el crédito y el saldo a favor.</param>
/// <param name="PeriodStart">Desde cuándo cuenta si se paga ahora.</param>
/// <param name="PeriodEnd">Hasta cuándo.</param>
public sealed record PackageAddOnQuote(PackageQuote Quote, Guid? ReplacesLineId, List<CreditedPeriod> CreditedPeriods, SubscriptionCharge Charge, Instant PeriodStart, Instant PeriodEnd)
{
    /// <summary>
    /// La cotización para la pantalla.
    /// </summary>
    /// <returns>El DTO.</returns>
    public PackageQuoteDto ToDto()
    {
        var dto = PackageQuoteDto.From(Quote);

        dto.ReplacesLineId = ReplacesLineId;
        dto.Credit = Charge.ProrationCredit.Amount;
        dto.CreditTax = CreditedPeriods.Sum(x => x.Tax?.Amount ?? 0);
        dto.CreditBalanceApplied = Charge.CreditBalanceApplied.Amount;
        dto.AmountDue = Charge.AmountDue.Amount;
        dto.RemainingCreditBalance = Charge.RemainingCreditBalance.Amount;
        dto.PeriodStart = PeriodStart;
        dto.PeriodEnd = PeriodEnd;

        return dto;
    }
}
