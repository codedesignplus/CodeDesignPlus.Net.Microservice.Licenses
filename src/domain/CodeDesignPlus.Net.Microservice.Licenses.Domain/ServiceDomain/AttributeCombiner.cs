using System.Globalization;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;

/// <summary>
/// Los atributos efectivos de una copropiedad: los de su licencia combinados con los de sus paquetes vigentes, cada uno con
/// la regla que declara el catálogo (pendings/739).
/// </summary>
/// <remarks>
/// Sum suma los enteros, Max toma el mayor y Or hace «o» de los sí/no. Si la licencia no trae la clave, cuenta como cero o
/// como «no». Un valor de la licencia que no es del tipo de la regla (un texto donde se espera un número) lo reemplaza el
/// del paquete: el paquete se compró para dar ese atributo. ms-tenants tiene la misma regla en su copia (comentario
/// cruzado en <c>EffectiveAttributes</c>).
/// </remarks>
public static class AttributeCombiner
{
    /// <summary>
    /// Combina los atributos.
    /// </summary>
    /// <param name="license">Los atributos de la licencia.</param>
    /// <param name="packages">Los atributos de los paquetes que cuentan.</param>
    /// <returns>Los atributos efectivos.</returns>
    public static Dictionary<string, string> Combine(IReadOnlyDictionary<string, string> license, IEnumerable<PackageAttribute> packages)
    {
        var result = new Dictionary<string, string>(license);

        foreach (var attribute in packages)
        {
            result[attribute.Key] = result.TryGetValue(attribute.Key, out var current)
                ? Merge(current, attribute.Value, attribute.Combination)
                : attribute.Value;
        }

        return result;
    }

    /// <summary>
    /// Combina dos valores con una regla.
    /// </summary>
    /// <param name="current">El valor que ya había.</param>
    /// <param name="added">El valor que se suma.</param>
    /// <param name="combination">La regla.</param>
    /// <returns>El valor combinado.</returns>
    public static string Merge(string current, string added, AttributeCombination combination)
    {
        if (combination == AttributeCombination.Or)
        {
            var a = bool.TryParse(current, out var left) && left;
            var b = bool.TryParse(added, out var right) && right;

            return a || b ? "true" : "false";
        }

        if (!long.TryParse(added, NumberStyles.Integer, CultureInfo.InvariantCulture, out var addedNumber))
        {
            return current;
        }

        if (!long.TryParse(current, NumberStyles.Integer, CultureInfo.InvariantCulture, out var currentNumber))
        {
            return added;
        }

        var value = combination == AttributeCombination.Sum ? currentNumber + addedNumber : Math.Max(currentNumber, addedNumber);

        return value.ToString(CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Si los atributos nuevos de un paquete son una ampliación de los que tenía: ninguno baja y alguno sube.
    /// </summary>
    /// <param name="current">Los atributos de la línea vigente.</param>
    /// <param name="next">Los atributos de la ampliación.</param>
    /// <returns><c>true</c> si amplía.</returns>
    public static bool IsUpgrade(IReadOnlyCollection<PackageAttribute> current, IReadOnlyCollection<PackageAttribute> next)
    {
        var better = false;
        var nextByKey = next.ToDictionary(x => x.Key, StringComparer.Ordinal);

        foreach (var attribute in current)
        {
            if (!nextByKey.TryGetValue(attribute.Key, out var replacement))
            {
                return false;
            }

            var compared = Compare(attribute.Value, replacement.Value, attribute.Combination);

            if (compared < 0)
            {
                return false;
            }

            better |= compared > 0;
        }

        return better || next.Any(x => current.All(y => y.Key != x.Key) && Compare(Zero(x.Combination), x.Value, x.Combination) > 0);
    }

    private static string Zero(AttributeCombination combination) => combination == AttributeCombination.Or ? "false" : "0";

    private static int Compare(string current, string next, AttributeCombination combination)
    {
        if (combination == AttributeCombination.Or)
        {
            var a = bool.TryParse(current, out var left) && left;
            var b = bool.TryParse(next, out var right) && right;

            return b.CompareTo(a);
        }

        long.TryParse(current, NumberStyles.Integer, CultureInfo.InvariantCulture, out var currentNumber);
        long.TryParse(next, NumberStyles.Integer, CultureInfo.InvariantCulture, out var nextNumber);

        return nextNumber.CompareTo(currentNumber);
    }
}
