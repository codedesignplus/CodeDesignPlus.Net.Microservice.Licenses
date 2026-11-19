using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;

/// <summary>
/// Lo que tiene que cumplir el contenido de un paquete para que se pueda cotizar siempre (pendings/739).
/// </summary>
/// <remarks>
/// Un fijo lleva atributos y ningún componente; un configurable, componentes y ningún atributo fijo. Cada clave aparece una
/// vez en todo el paquete (contando las <see cref="PackageComponent.EnableKey"/>), y cada valor va con su combinación.
/// </remarks>
public static class PackageRules
{
    /// <summary>
    /// La longitud máxima de una clave de atributo: la de un nombre (regla 48).
    /// </summary>
    public static int KeyLength => FieldLength.Name;

    /// <summary>
    /// Comprueba el contenido del paquete.
    /// </summary>
    /// <param name="definition">Lo que define el paquete.</param>
    public static void EnsureContent(PackageDefinition definition)
    {
        DomainGuard.IsNull(definition.Attributes, Errors.FixedPackageRequiresAttributes);
        DomainGuard.IsNull(definition.Components, Errors.ConfigurablePackageRequiresComponents);

        if (definition.Kind == PackageKind.Fixed)
        {
            DomainGuard.IsTrue(definition.Attributes.Count == 0 || definition.Components.Count > 0, Errors.FixedPackageRequiresAttributes);

            EnsureAttributes(definition.Attributes);

            return;
        }

        DomainGuard.IsTrue(definition.Components.Count == 0 || definition.Attributes.Count > 0, Errors.ConfigurablePackageRequiresComponents);

        EnsureComponents(definition.Components);
    }

    private static void EnsureAttributes(List<PackageAttribute> attributes)
    {
        foreach (var attribute in attributes)
        {
            EnsureKey(attribute?.Key);
            DomainGuard.IsFalse(Enum.IsDefined(attribute!.Combination), Errors.PackageAttributeCombinationIsInvalid.With(attribute.Key));
            DomainGuard.IsFalse(attribute.HasValidValue(), Errors.PackageAttributeValueIsInvalid.With(attribute.Key));
        }

        EnsureUnique(attributes.Select(x => x.Key.Trim()));
    }

    private static void EnsureComponents(List<PackageComponent> components)
    {
        var quantities = components.Where(x => x is not null && x.Kind == PackageComponentKind.Quantity).Select(x => x.Key.Trim()).ToHashSet(StringComparer.Ordinal);

        foreach (var component in components)
        {
            EnsureKey(component?.Key);

            var key = component!.Key.Trim();

            DomainGuard.IsFalse(Enum.IsDefined(component.Kind), Errors.PackageSelectionIsInvalid.With(key));
            DomainGuard.IsFalse(Enum.IsDefined(component.Combination), Errors.PackageAttributeCombinationIsInvalid.With(key));

            switch (component.Kind)
            {
                case PackageComponentKind.Quantity:
                    EnsureRange(component, key);
                    DomainGuard.IsTrue((component.Tiers ?? []).Count > 0, Errors.PackageSelectionIsInvalid.With(key));
                    EnsureNoMultiplier(component, key);

                    break;

                case PackageComponentKind.Volume:
                    EnsureRange(component, key);
                    EnsureVolumeTiers(component, key);
                    DomainGuard.IsTrue(component.Multiplier is { } multiplier && (multiplier < 1 || component.Max > long.MaxValue / multiplier), Errors.PackageComponentMultiplierIsInvalid.With(key));
                    // Lo consumible se amplía sumando unidades: solo tiene sentido en un atributo que se suma.
                    DomainGuard.IsTrue(component.Consumable == true && component.Combination != AttributeCombination.Sum, Errors.PackageConsumableComponentIsInvalid.With(key));

                    break;

                case PackageComponentKind.Tiered:
                    var tiers = component.Tiers ?? [];

                    DomainGuard.IsTrue(component.Combination == AttributeCombination.Or, Errors.PackageAttributeValueIsInvalid.With(key));
                    DomainGuard.IsTrue(tiers.Count == 0 || tiers.Any(x => x is null || x.Value <= 0) || tiers.Select(x => x.Value).Distinct().Count() != tiers.Count || tiers.All(x => x.Value != component.Default), Errors.PackageComponentTiersAreInvalid.With(key));
                    DomainGuard.IsFalse(string.IsNullOrWhiteSpace(component.EnableKey), Errors.PackageSelectionIsInvalid.With(key));
                    EnsurePerUnitOf(component, key, quantities);
                    EnsureNoMultiplier(component, key);

                    break;

                default:
                    DomainGuard.IsFalse(component.Combination == AttributeCombination.Or, Errors.PackageAttributeValueIsInvalid.With(key));
                    DomainGuard.IsTrue(component.Default is not (0 or 1) || (component.Required && component.Default != 1), Errors.PackageSelectionIsInvalid.With(key));
                    DomainGuard.IsTrue((component.Tiers ?? []).Count > 0 || !string.IsNullOrWhiteSpace(component.EnableKey), Errors.PackageSelectionIsInvalid.With(key));
                    EnsurePerUnitOf(component, key, quantities);
                    EnsureNoMultiplier(component, key);

                    break;
            }
        }

        EnsureUnique(components.SelectMany(x => string.IsNullOrWhiteSpace(x.EnableKey) ? new[] { x.Key.Trim() } : [x.Key.Trim(), x.EnableKey.Trim()]));
        EnsureConsumables(components);
    }

    /// <summary>
    /// Lo que comparten la cantidad y la cantidad por tramos de volumen: un entero que se suma o del que se toma el mayor,
    /// con su rango y su paso, que no se cobra por unidades de otro componente ni va «siempre encendido».
    /// </summary>
    private static void EnsureRange(PackageComponent component, string key)
    {
        DomainGuard.IsTrue(component.Combination == AttributeCombination.Or, Errors.PackageAttributeValueIsInvalid.With(key));
        DomainGuard.IsTrue(component.Min < 0 || component.Max < component.Min || component.Step <= 0 || component.Default < component.Min || component.Default > component.Max || (component.Default - component.Min) % component.Step != 0, Errors.PackageComponentRangeIsInvalid.With(key));
        DomainGuard.IsFalse(string.IsNullOrWhiteSpace(component.PerUnitOf), Errors.PackageComponentPerUnitOfIsInvalid.With(key));
        DomainGuard.IsTrue(component.Required, Errors.PackageSelectionIsInvalid.With(key));

        if (!string.IsNullOrWhiteSpace(component.EnableKey))
        {
            EnsureKey(component.EnableKey);
        }
    }

    /// <summary>
    /// Los tramos de volumen tienen que poner precio a toda cantidad que se pueda elegir (pendings/744): al menos uno, cada
    /// uno desde una cantidad distinta y mayor que cero, el primero desde la menor cantidad elegible o antes, y ninguno más
    /// allá del máximo. El precio sale solo de ellos: el componente no lleva precio unitario propio.
    /// </summary>
    private static void EnsureVolumeTiers(PackageComponent component, string key)
    {
        var tiers = component.Tiers ?? [];
        var error = Errors.PackageComponentVolumeTiersAreInvalid.With(key);

        DomainGuard.IsTrue(tiers.Count == 0 || tiers.Any(x => x is null || x.Value <= 0), error);
        DomainGuard.IsTrue(tiers.Select(x => x.Value).Distinct().Count() != tiers.Count, error);

        // La menor cantidad que se cobra: cero no entrega nada y no necesita tramo.
        var smallest = component.Min > 0 ? component.Min : component.Step;

        DomainGuard.IsTrue(tiers.Min(x => x.Value) > smallest || tiers.Max(x => x.Value) > component.Max, error);
        DomainGuard.IsTrue(component.UnitPrice.Amount != 0, error);
    }

    /// <summary>
    /// Solo una cantidad por tramos de volumen entrega un múltiplo de lo elegido; en los demás, 1 es lo mismo que nada.
    /// </summary>
    private static void EnsureNoMultiplier(PackageComponent component, string key)
    {
        DomainGuard.IsTrue(component.Multiplier is not (null or 1), Errors.PackageComponentMultiplierIsInvalid.With(key));
    }

    /// <summary>
    /// Solo una cantidad por tramos de volumen es consumible, y un paquete no mezcla consumibles con lo demás (pendings/744):
    /// ampliar un consumible cobra solo lo añadido y no acredita tiempo, y ampliar lo demás cobra el año y acredita lo no
    /// usado; una misma línea no puede hacer las dos cosas.
    /// </summary>
    private static void EnsureConsumables(List<PackageComponent> components)
    {
        var wrong = components.FirstOrDefault(x => x.Consumable == true && x.Kind != PackageComponentKind.Volume);

        DomainGuard.IsTrue(wrong is not null, Errors.PackageConsumableComponentIsInvalid.With(wrong?.Key.Trim() ?? string.Empty));

        var other = components.Any(x => x.Consumable == true) ? components.FirstOrDefault(x => x.Consumable != true) : null;

        DomainGuard.IsTrue(other is not null, Errors.PackageMixesConsumableComponents.With(other?.Key.Trim() ?? string.Empty));
    }

    private static void EnsurePerUnitOf(PackageComponent component, string key, HashSet<string> quantities)
    {
        if (string.IsNullOrWhiteSpace(component.PerUnitOf))
        {
            return;
        }

        DomainGuard.IsFalse(quantities.Contains(component.PerUnitOf.Trim()), Errors.PackageComponentPerUnitOfIsInvalid.With(key));
    }

    private static void EnsureKey(string? key)
    {
        DomainGuard.IsTrue(string.IsNullOrWhiteSpace(key) || key.Trim().Length > KeyLength, Errors.PackageAttributeKeyIsRequired.With(KeyLength));
    }

    private static void EnsureUnique(IEnumerable<string> keys)
    {
        var repeated = keys.GroupBy(x => x, StringComparer.Ordinal).FirstOrDefault(g => g.Count() > 1);

        DomainGuard.IsTrue(repeated is not null, Errors.PackageAttributeKeyIsRepeated.With(repeated?.Key ?? string.Empty));
    }
}
