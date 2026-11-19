using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Un tramo de un componente por tramos, con su precio (pendings/739).
/// </summary>
/// <remarks>
/// En un componente por tramos de volumen (<see cref="Enums.PackageComponentKind.Volume"/>, pendings/744) el tramo no es un
/// valor que se elige sino un rango: <paramref name="Value"/> es la cantidad desde la que aplica, hasta donde empieza el
/// siguiente, y <paramref name="Price"/> el precio de cada unidad entregada.
/// </remarks>
/// <param name="Value">El valor del atributo en ese tramo (p. ej. 200 conectados), o la cantidad desde la que aplica un tramo de volumen.</param>
/// <param name="Price">Su precio anual, por unidad de <see cref="PackageComponent.PerUnitOf"/> si lo tiene; en un tramo de volumen, el de cada unidad entregada.</param>
public sealed record PackageTier(long Value, Money Price);
