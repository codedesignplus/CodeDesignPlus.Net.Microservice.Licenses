using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Commands;

/// <summary>
/// Convierte lo que escribe el Super Admin en lo que define un paquete: los precios a unidades mínimas con los decimales de
/// la moneda resueltos por ms-locations, y la tarifa a puntos básicos (reglas 04, 05 y 09).
/// </summary>
public static class PackageDefinitions
{
    /// <summary>
    /// Arma la definición.
    /// </summary>
    /// <returns>La definición del paquete.</returns>
    public static async Task<PackageDefinition> BuildAsync(ICurrencyGrpc currencyGrpc, string name, string shortDescription, string description, Icon icon, bool isActive, bool isPopular, bool showInLandingPage, List<OfferedTenant>? offeredTo, PackageKind kind, string currency, decimal basePrice, decimal minimumPrice, TaxTreatment taxTreatment, decimal taxPercentage, string? taxLegalBasis, string invoiceConcept, List<PackageAttributeInput>? attributes, List<PackageComponentInput>? components, CancellationToken cancellationToken)
    {
        var code = currency.Trim().ToUpperInvariant();
        var resolved = await currencyGrpc.GetCurrencyAsync(code: code, cancellationToken: cancellationToken);
        var digits = (short)resolved.DecimalDigits;

        return new PackageDefinition(
            name,
            shortDescription,
            description,
            icon,
            isActive,
            isPopular,
            showInLandingPage,
            offeredTo ?? [],
            kind,
            code,
            new MoneyInput(basePrice, code).ToMoney(digits),
            new MoneyInput(minimumPrice, code).ToMoney(digits),
            taxTreatment,
            BasisPoints.FromPercentage(taxPercentage),
            taxLegalBasis,
            invoiceConcept,
            [.. (attributes ?? []).Select(x => x.ToAttribute())],
            [.. (components ?? []).Select(x => x.ToComponent(code, digits))]);
    }
}
