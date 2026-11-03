using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Services;

/// <summary>
/// Cotiza los paquetes que se piden junto con una licencia: en la compra, la renovación y el cambio (pendings/739).
/// </summary>
public static class PackageSelections
{
    /// <summary>
    /// Cotiza cada paquete pedido, comprobando que se pueda vender así.
    /// </summary>
    /// <param name="repository">El catálogo de paquetes.</param>
    /// <param name="selections">Los paquetes pedidos.</param>
    /// <param name="currency">La moneda de la licencia: los paquetes tienen que tener la misma.</param>
    /// <param name="isAllowed">Si el paquete se puede vender en este camino (a la venta, u ofrecido a la copropiedad).</param>
    /// <param name="notAllowed">El error si no.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Las cotizaciones, en el orden pedido.</returns>
    public static async Task<List<PackageQuote>> QuoteAsync(IPackageRepository repository, IReadOnlyCollection<PackageSelectionInput>? selections, string currency, Func<PackageAggregate, bool> isAllowed, CodeDesignPlus.Net.Exceptions.Error notAllowed, CancellationToken cancellationToken)
    {
        var requested = selections ?? [];

        ApplicationGuard.IsTrue(requested.Any(x => x is null || x.PackageId == Guid.Empty), Errors.PackageNotFound);
        ApplicationGuard.IsTrue(requested.GroupBy(x => x.PackageId).Any(g => g.Count() > 1), Errors.PackageIsRepeated);

        var quotes = new List<PackageQuote>();

        foreach (var selection in requested)
        {
            var package = await repository.FindAsync<PackageAggregate>(selection.PackageId, cancellationToken);

            ApplicationGuard.IsTrue(package is null || package.IsDeleted, Errors.PackageNotFound);
            ApplicationGuard.IsFalse(isAllowed(package!), notAllowed);
            ApplicationGuard.IsTrue(package!.Currency != currency, Errors.PackageCurrencyDoesNotMatch);

            quotes.Add(PackagePricing.Quote(package, selection.Selections));
        }

        return quotes;
    }
}
