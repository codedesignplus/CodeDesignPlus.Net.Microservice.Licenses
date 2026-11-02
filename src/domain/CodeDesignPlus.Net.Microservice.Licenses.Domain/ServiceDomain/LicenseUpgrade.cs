using System.Globalization;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;

/// <summary>
/// Si una licencia es mayor que la que tiene la copropiedad, y qué gana con ella (pendings/738).
/// </summary>
/// <remarks>
/// Solo se cambia hacia arriba (decisión del usuario, 2026-11-02): la licencia destino tiene todos los módulos de la actual
/// y sus atributos iguales o mayores, y algo más (un módulo, o un atributo mayor o nuevo).
/// <para>
/// Un atributo se compara según su valor: un número, que el destino iguale o supere; un sí/no, que el destino no apague lo
/// que la actual tiene encendido. Un texto («Ideal para», «Soporte») describe la licencia y no limita nada, así que no
/// cuenta para decir si es mayor. La comparación no conoce ninguna clave en particular: sirve igual a cualquier producto.
/// </para>
/// </remarks>
public static class LicenseUpgrade
{
    /// <summary>
    /// Compara la licencia de la copropiedad con una licencia del catálogo.
    /// </summary>
    /// <param name="current">La copia de la licencia que tiene la copropiedad.</param>
    /// <param name="target">La licencia del catálogo.</param>
    /// <returns>Si es mayor, con los módulos y atributos que gana.</returns>
    public static LicenseUpgradeResult Compare(License current, LicenseAggregate target)
    {
        if (target.Id == current.Id)
        {
            return LicenseUpgradeResult.NotAnUpgrade;
        }

        var targetModules = target.Modules.Select(x => x.Id).ToHashSet();

        if (!current.Modules.All(x => targetModules.Contains(x.Id)))
        {
            return LicenseUpgradeResult.NotAnUpgrade;
        }

        var gainedAttributes = new Dictionary<string, string>();

        foreach (var (key, value) in current.Attributes)
        {
            if (!Covers(value, target.Attributes.GetValueOrDefault(key), out var better))
            {
                return LicenseUpgradeResult.NotAnUpgrade;
            }

            if (better)
            {
                gainedAttributes[key] = target.Attributes[key];
            }
        }

        foreach (var (key, value) in target.Attributes.Where(x => !current.Attributes.ContainsKey(x.Key) && IsLimit(x.Value)))
        {
            gainedAttributes[key] = value;
        }

        var currentModules = current.Modules.Select(x => x.Id).ToHashSet();
        var gainedModules = target.Modules.Where(x => !currentModules.Contains(x.Id)).ToList();

        if (gainedModules.Count == 0 && gainedAttributes.Count == 0)
        {
            return LicenseUpgradeResult.NotAnUpgrade;
        }

        return new LicenseUpgradeResult(true, gainedModules, gainedAttributes);
    }

    /// <summary>
    /// Si el valor del destino cubre el actual, y si lo supera.
    /// </summary>
    private static bool Covers(string current, string? target, out bool better)
    {
        better = false;

        if (TryNumber(current, out var currentNumber))
        {
            if (target is null || !TryNumber(target, out var targetNumber) || targetNumber < currentNumber)
            {
                return false;
            }

            better = targetNumber > currentNumber;

            return true;
        }

        if (bool.TryParse(current, out var currentFlag))
        {
            if (target is null || !bool.TryParse(target, out var targetFlag) || (currentFlag && !targetFlag))
            {
                return false;
            }

            better = targetFlag && !currentFlag;

            return true;
        }

        // Un texto describe la licencia: no limita nada, así que no la hace menor ni mayor.
        return true;
    }

    private static bool IsLimit(string value) => TryNumber(value, out _) || bool.TryParse(value, out _);

    private static bool TryNumber(string value, out decimal number) => decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out number);
}

/// <summary>
/// El resultado de comparar una licencia con la de la copropiedad.
/// </summary>
/// <param name="IsUpgrade">Si la licencia es mayor.</param>
/// <param name="GainedModules">Los módulos que la copropiedad no tiene.</param>
/// <param name="GainedAttributes">Los atributos mayores o nuevos, con el valor del destino.</param>
public sealed record LicenseUpgradeResult(bool IsUpgrade, List<ModuleEntity> GainedModules, Dictionary<string, string> GainedAttributes)
{
    /// <summary>
    /// Una licencia que no es mayor.
    /// </summary>
    public static LicenseUpgradeResult NotAnUpgrade => new(false, [], []);
}
