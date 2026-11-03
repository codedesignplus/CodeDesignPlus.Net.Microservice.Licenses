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
                    DomainGuard.IsTrue(component.Combination == AttributeCombination.Or, Errors.PackageAttributeValueIsInvalid.With(key));
                    DomainGuard.IsTrue(component.Min < 0 || component.Max < component.Min || component.Step <= 0 || component.Default < component.Min || component.Default > component.Max || (component.Default - component.Min) % component.Step != 0, Errors.PackageComponentRangeIsInvalid.With(key));
                    DomainGuard.IsFalse(string.IsNullOrWhiteSpace(component.PerUnitOf), Errors.PackageComponentPerUnitOfIsInvalid.With(key));
                    DomainGuard.IsTrue((component.Tiers ?? []).Count > 0 || component.Required, Errors.PackageSelectionIsInvalid.With(key));

                    if (!string.IsNullOrWhiteSpace(component.EnableKey))
                    {
                        EnsureKey(component.EnableKey);
                    }

                    break;

                case PackageComponentKind.Tiered:
                    var tiers = component.Tiers ?? [];

                    DomainGuard.IsTrue(component.Combination == AttributeCombination.Or, Errors.PackageAttributeValueIsInvalid.With(key));
                    DomainGuard.IsTrue(tiers.Count == 0 || tiers.Any(x => x is null || x.Value <= 0) || tiers.Select(x => x.Value).Distinct().Count() != tiers.Count || tiers.All(x => x.Value != component.Default), Errors.PackageComponentTiersAreInvalid.With(key));
                    DomainGuard.IsFalse(string.IsNullOrWhiteSpace(component.EnableKey), Errors.PackageSelectionIsInvalid.With(key));
                    EnsurePerUnitOf(component, key, quantities);

                    break;

                default:
                    DomainGuard.IsFalse(component.Combination == AttributeCombination.Or, Errors.PackageAttributeValueIsInvalid.With(key));
                    DomainGuard.IsTrue(component.Default is not (0 or 1) || (component.Required && component.Default != 1), Errors.PackageSelectionIsInvalid.With(key));
                    DomainGuard.IsTrue((component.Tiers ?? []).Count > 0 || !string.IsNullOrWhiteSpace(component.EnableKey), Errors.PackageSelectionIsInvalid.With(key));
                    EnsurePerUnitOf(component, key, quantities);

                    break;
            }
        }

        EnsureUnique(components.SelectMany(x => string.IsNullOrWhiteSpace(x.EnableKey) ? new[] { x.Key.Trim() } : [x.Key.Trim(), x.EnableKey.Trim()]));
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
