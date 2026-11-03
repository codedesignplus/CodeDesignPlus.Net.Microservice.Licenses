using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Queries.QuotePackage;

/// <summary>
/// Cotiza un paquete con lo que elige el cliente (pendings/739). Sin copropiedad, el precio del catálogo; con copropiedad,
/// además el crédito de una ampliación, el saldo a favor y el periodo.
/// </summary>
/// <param name="PackageId">El paquete.</param>
/// <param name="Selections">Lo elegido por clave de componente.</param>
/// <param name="TenantId">La copropiedad de la sesión; vacía en la landing sin sesión.</param>
public record QuotePackageQuery(Guid PackageId, Dictionary<string, string>? Selections, Guid TenantId) : IRequest<PackageQuoteDto>;

/// <summary>
/// Cotiza un paquete.
/// </summary>
public class QuotePackageQueryHandler(IPackageRepository packages, IOrderRepository orders, ICreditBalanceRepository creditBalances) : IRequestHandler<QuotePackageQuery, PackageQuoteDto>
{
    /// <inheritdoc/>
    public async Task<PackageQuoteDto> Handle(QuotePackageQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var package = await packages.FindAsync<PackageAggregate>(request.PackageId, cancellationToken);

        ApplicationGuard.IsTrue(package is null || package.IsDeleted, Errors.PackageNotFound);

        var state = request.TenantId == Guid.Empty ? null : await TenantPackages.LoadAsync(orders, request.TenantId, cancellationToken);

        if (state is null)
        {
            // La landing: solo lo que está a la venta, por su precio completo.
            ApplicationGuard.IsFalse(package!.IsForSale, Errors.PackageNotFound);

            return PackageQuoteDto.From(PackagePricing.Quote(package, request.Selections));
        }

        var now = SystemClock.Instance.GetCurrentInstant();
        var currency = state.Subscription.CurrentAt(now).License.Total.Currency;
        var balance = await creditBalances.FindAsync<CreditBalanceAggregate>(request.TenantId, request.TenantId, cancellationToken);
        var awaiting = await orders.FindAwaitingPackagePaymentAsync(request.TenantId, cancellationToken);
        var available = SubscriptionCheckout.AvailableCreditBalance(balance, awaiting, currency);

        return state.QuoteAddOn(package!, request.Selections, available, now).ToDto();
    }
}
