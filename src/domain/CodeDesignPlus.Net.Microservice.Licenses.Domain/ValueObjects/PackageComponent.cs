using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Lo que el cliente elige en un paquete configurable, con su rango y su precio (pendings/739).
/// </summary>
/// <remarks>
/// El componente no sabe qué es una asamblea: es una clave de atributo con un tipo y un precio. La etiqueta legible la
/// pone la pantalla, como con los atributos de la licencia.
/// </remarks>
/// <param name="Key">La clave del atributo que fija.</param>
/// <param name="Kind">Cantidad, tramos o sí/no.</param>
/// <param name="Combination">Cómo se combina su atributo con el de la licencia.</param>
/// <param name="Min">El mínimo de una cantidad.</param>
/// <param name="Max">El máximo de una cantidad.</param>
/// <param name="Step">El paso de una cantidad.</param>
/// <param name="Default">El valor por defecto: la cantidad, el valor del tramo o 0/1 en un sí/no.</param>
/// <param name="UnitPrice">El precio anual de una unidad (cantidad) o del sí encendido.</param>
/// <param name="PerUnitOf">La clave de una cantidad por cuyas unidades se multiplica el precio de un tramo o de un sí.</param>
/// <param name="EnableKey">Un atributo sí/no que queda encendido cuando la cantidad es mayor que cero.</param>
/// <param name="Required">Un sí/no que va siempre encendido.</param>
/// <param name="Tiers">Los tramos, de menor a mayor.</param>
public sealed record PackageComponent(string Key, PackageComponentKind Kind, AttributeCombination Combination, long Min, long Max, long Step, long Default, Money UnitPrice, string? PerUnitOf, string? EnableKey, bool Required, List<PackageTier> Tiers);
