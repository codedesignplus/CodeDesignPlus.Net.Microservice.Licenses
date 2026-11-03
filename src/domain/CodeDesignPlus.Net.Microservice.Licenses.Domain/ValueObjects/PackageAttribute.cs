using System.Globalization;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Un atributo que aporta un paquete, con cómo se combina con el de la licencia (pendings/739).
/// </summary>
/// <param name="Key">La clave del atributo, la misma que usa la licencia.</param>
/// <param name="Value">El valor como texto: un entero para Sum y Max, <c>true</c> o <c>false</c> para Or.</param>
/// <param name="Combination">Cómo se combina.</param>
public sealed record PackageAttribute(string Key, string Value, AttributeCombination Combination)
{
    /// <summary>
    /// Si el valor sirve para su combinación: un entero no negativo para Sum y Max, un sí/no para Or.
    /// </summary>
    /// <returns><c>true</c> si es válido.</returns>
    public bool HasValidValue() => Combination switch
    {
        AttributeCombination.Sum or AttributeCombination.Max => long.TryParse(Value, NumberStyles.None, CultureInfo.InvariantCulture, out _),
        AttributeCombination.Or => bool.TryParse(Value, out _),
        _ => false
    };
}
