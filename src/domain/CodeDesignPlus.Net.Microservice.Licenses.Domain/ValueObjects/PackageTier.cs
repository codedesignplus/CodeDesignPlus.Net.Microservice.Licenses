using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Un tramo de un componente por tramos, con su precio (pendings/739).
/// </summary>
/// <param name="Value">El valor del atributo en ese tramo (p. ej. 200 conectados).</param>
/// <param name="Price">Su precio anual, por unidad de <see cref="PackageComponent.PerUnitOf"/> si lo tiene.</param>
public sealed record PackageTier(long Value, Money Price);
