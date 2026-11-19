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
/// <param name="Kind">Cantidad, tramos, sí/no o cantidad por tramos de volumen.</param>
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
public sealed record PackageComponent(string Key, PackageComponentKind Kind, AttributeCombination Combination, long Min, long Max, long Step, long Default, Money UnitPrice, string? PerUnitOf, string? EnableKey, bool Required, List<PackageTier> Tiers)
{
    /// <summary>
    /// Cuántas unidades del atributo entrega cada unidad elegida en una cantidad por tramos de volumen (pendings/744): con
    /// 12, quien elige 50 al mes recibe 600 para el año, y esas 600 son las que se cobran. Nulo vale 1.
    /// </summary>
    /// <remarks>
    /// Es un miembro suelto y no un argumento del constructor porque los paquetes ya guardados no lo traen (regla 31).
    /// </remarks>
    public long? Multiplier { get; init; }

    /// <summary>
    /// Las unidades del atributo que entrega lo elegido: lo elegido por el multiplicador.
    /// </summary>
    /// <param name="value">Lo elegido.</param>
    /// <returns>Las unidades entregadas, que son también las cobradas.</returns>
    public long DeliveredUnits(long value) => value * (Multiplier ?? 1);

    /// <summary>
    /// El tramo de volumen en que cae lo elegido: el que empieza más arriba sin pasarse.
    /// </summary>
    /// <param name="value">Lo elegido.</param>
    /// <returns>El tramo, o <c>null</c> si lo elegido queda por debajo del primero.</returns>
    public PackageTier? VolumeTierOf(long value) => (Tiers ?? []).Where(x => x.Value <= value).MaxBy(x => x.Value);
}
