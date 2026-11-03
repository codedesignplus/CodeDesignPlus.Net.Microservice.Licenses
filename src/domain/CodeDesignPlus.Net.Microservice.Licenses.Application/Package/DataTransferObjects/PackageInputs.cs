using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;

/// <summary>
/// Escritura de un atributo de un paquete fijo (pendings/739).
/// </summary>
/// <param name="Key">La clave.</param>
/// <param name="Value">El valor como texto.</param>
/// <param name="Combine">Cómo se combina.</param>
public sealed record PackageAttributeInput(string Key, string Value, AttributeCombination Combine)
{
    /// <summary>
    /// El atributo del dominio.
    /// </summary>
    /// <returns>El atributo.</returns>
    public PackageAttribute ToAttribute() => new(Key ?? string.Empty, Value ?? string.Empty, Combine);
}

/// <summary>
/// Escritura de un tramo: el precio en unidad mayor (regla 04).
/// </summary>
/// <param name="Value">El valor del tramo.</param>
/// <param name="Price">El precio en unidad mayor.</param>
public sealed record PackageTierInput(long Value, decimal Price);

/// <summary>
/// Escritura de un componente de un paquete configurable: los precios en unidad mayor (regla 04).
/// </summary>
/// <param name="Key">La clave del atributo.</param>
/// <param name="Kind">Cantidad, tramos o sí/no.</param>
/// <param name="Combine">Cómo se combina.</param>
/// <param name="Min">El mínimo de una cantidad.</param>
/// <param name="Max">El máximo de una cantidad.</param>
/// <param name="Step">El paso de una cantidad.</param>
/// <param name="Default">El valor por defecto.</param>
/// <param name="UnitPrice">El precio de una unidad o del sí, en unidad mayor.</param>
/// <param name="PerUnitOf">La cantidad por cuyas unidades se multiplica el precio.</param>
/// <param name="EnableKey">El sí/no que enciende una cantidad mayor que cero.</param>
/// <param name="Required">Si el sí/no va siempre encendido.</param>
/// <param name="Tiers">Los tramos.</param>
public sealed record PackageComponentInput(string Key, PackageComponentKind Kind, AttributeCombination Combine, long Min, long Max, long Step, long Default, decimal UnitPrice, string? PerUnitOf, string? EnableKey, bool Required, List<PackageTierInput>? Tiers)
{
    /// <summary>
    /// El componente del dominio con los precios en unidades mínimas.
    /// </summary>
    /// <param name="currency">La moneda del paquete.</param>
    /// <param name="decimalDigits">Los decimales de la moneda.</param>
    /// <returns>El componente.</returns>
    public PackageComponent ToComponent(string currency, short decimalDigits) => new(
        Key ?? string.Empty,
        Kind,
        Combine,
        Min,
        Max,
        Step,
        Default,
        new MoneyInput(UnitPrice, currency).ToMoney(decimalDigits),
        PerUnitOf,
        EnableKey,
        Required,
        [.. (Tiers ?? []).Select(x => new PackageTier(x.Value, new MoneyInput(x.Price, currency).ToMoney(decimalDigits)))]);
}

/// <summary>
/// Un paquete elegido en una compra, una renovación o una cotización (pendings/739).
/// </summary>
/// <param name="PackageId">El paquete del catálogo.</param>
/// <param name="Selections">Lo elegido por clave de componente, como texto; vacío en un fijo.</param>
public sealed record PackageSelectionInput(Guid PackageId, Dictionary<string, string>? Selections);
