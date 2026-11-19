using System.Globalization;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;

/// <summary>
/// El precio de un paquete con lo que elige el cliente, y los atributos que aporta (pendings/739).
/// </summary>
/// <remarks>
/// Es el único cálculo del precio: lo usan la cotización, la compra, la ampliación y la renovación, así que lo cobrado es
/// siempre lo cotizado (regla 06).
/// <list type="bullet">
/// <item>Un fijo cuesta su precio y aporta sus atributos; no se elige nada.</item>
/// <item>En un configurable, cada componente toma lo elegido o su valor por defecto. Una cantidad cuesta sus unidades por
/// su precio unitario; un tramo, su precio; un sí encendido, su precio. El tramo y el sí con <c>PerUnitOf</c> se
/// multiplican por las unidades de esa cantidad. Se suma la base y, si el total queda por debajo del mínimo, se cobra el
/// mínimo.</item>
/// <item>Una cantidad por tramos de volumen (pendings/744) entrega lo elegido por su multiplicador y cobra cada unidad
/// entregada al precio del tramo en que cae lo elegido: toda la cantidad a ese precio, no cada parte al de su tramo. Lo
/// elegido se guarda como se eligió; el atributo lleva lo entregado.</item>
/// <item>El impuesto se calcula sobre la base con la tarifa del paquete.</item>
/// </list>
/// </remarks>
public static class PackagePricing
{
    /// <summary>
    /// Cotiza el paquete.
    /// </summary>
    /// <param name="package">El paquete del catálogo.</param>
    /// <param name="selections">Lo elegido por clave de componente, como texto; lo que falte toma su valor por defecto.</param>
    /// <returns>La cotización.</returns>
    public static PackageQuote Quote(PackageAggregate package, IReadOnlyDictionary<string, string>? selections)
    {
        DomainGuard.IsNull(package, Errors.PackageIdIsRequired);

        var chosen = selections ?? new Dictionary<string, string>();

        return package.Kind == PackageKind.Fixed ? QuoteFixed(package, chosen) : QuoteConfigurable(package, chosen);
    }

    private static PackageQuote QuoteFixed(PackageAggregate package, IReadOnlyDictionary<string, string> chosen)
    {
        // Un fijo no se elige: cualquier selección es un error de quien llama, no algo que se ignore en silencio.
        DomainGuard.IsTrue(chosen.Count > 0, Errors.PackageSelectionIsUnknown.With(chosen.Keys.FirstOrDefault() ?? string.Empty));

        var (subTotal, tax, total) = package.WithTax(package.BasePrice);

        return new PackageQuote(package, [], [], package.BasePrice, false, subTotal, tax, total, [.. package.Attributes]);
    }

    private static PackageQuote QuoteConfigurable(PackageAggregate package, IReadOnlyDictionary<string, string> chosen)
    {
        var unknown = chosen.Keys.FirstOrDefault(key => package.Components.All(x => x.Key != key));

        DomainGuard.IsTrue(unknown is not null, Errors.PackageSelectionIsUnknown.With(unknown ?? string.Empty));

        var values = package.Components.ToDictionary(x => x.Key, x => ValueOf(x, chosen));
        var lines = new List<PackageQuoteLine>();
        var attributes = new List<PackageAttribute>();
        var componentsTotal = Money.Zero(package.Currency);

        foreach (var component in package.Components)
        {
            var value = values[component.Key];
            var units = UnitsOf(component, values);
            var unitPrice = UnitPriceOf(component, value);
            var amount = component.Kind == PackageComponentKind.Toggle && value == 0 ? Money.Zero(package.Currency) : unitPrice * units;

            lines.Add(new PackageQuoteLine(component.Key, Text(component, value), units, unitPrice, amount));
            attributes.Add(new PackageAttribute(component.Key, Granted(component, value), component.Combination));

            if (component.Kind is PackageComponentKind.Quantity or PackageComponentKind.Volume && component.EnableKey is not null)
            {
                attributes.Add(new PackageAttribute(component.EnableKey, Bool(value > 0), AttributeCombination.Or));
            }

            componentsTotal += amount;
        }

        var selected = package.BasePrice + componentsTotal;

        DomainGuard.IsTrue(selected.Amount == 0, Errors.PackageSelectionIsEmpty);

        var minimumApplied = selected.Amount < package.MinimumPrice.Amount;
        var (subTotal, tax, total) = package.WithTax(minimumApplied ? package.MinimumPrice : selected);
        var normalized = package.Components.ToDictionary(x => x.Key, x => Text(x, values[x.Key]));

        return new PackageQuote(package, normalized, lines, package.BasePrice, minimumApplied, subTotal, tax, total, attributes);
    }

    /// <summary>
    /// Cotiza lo que se le suma a un paquete consumible que la copropiedad ya tiene (pendings/744).
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    /// <item>Lo elegido es el <b>total nuevo</b>, como en cualquier ampliación; lo que se cobra son solo las unidades
    /// añadidas: lo que entrega el total menos lo que ya entrega la línea vigente.</item>
    /// <item><b>El precio de lo añadido es el del tramo en que queda el total</b>, todo lo añadido a ese precio. Es la
    /// lectura de «al precio del tramo en que quedó la copropiedad» (pendings/663); lo ya pagado no se recalcula ni se
    /// devuelve. Si la regla cambia (p. ej. el tramo de lo añadido), cambia solo <c>UnitPriceOf(component, total)</c>.</item>
    /// <item>Sin base, sin mínimo y sin crédito por tiempo: un cupo se gasta, no transcurre.</item>
    /// <item>Lo que ya tiene sale de los atributos de su línea y no de lo que eligió: así un cambio del multiplicador en el
    /// catálogo no le quita ni le regala unidades.</item>
    /// </list>
    /// </remarks>
    /// <param name="package">El paquete del catálogo.</param>
    /// <param name="selections">El total nuevo por clave de componente; lo que falte toma su valor por defecto.</param>
    /// <param name="held">Los atributos de la línea vigente de ese paquete.</param>
    /// <returns>La cotización de lo añadido, con los atributos y lo elegido del total.</returns>
    public static PackageQuote QuoteAddition(PackageAggregate package, IReadOnlyDictionary<string, string>? selections, IReadOnlyCollection<PackageAttribute> held)
    {
        DomainGuard.IsNull(package, Errors.PackageIdIsRequired);
        DomainGuard.IsFalse(package.IsConsumable, Errors.PackageAdditionIsInvalid.With(package.Name));

        var chosen = selections ?? new Dictionary<string, string>();
        var unknown = chosen.Keys.FirstOrDefault(key => package.Components.All(x => x.Key != key));

        DomainGuard.IsTrue(unknown is not null, Errors.PackageSelectionIsUnknown.With(unknown ?? string.Empty));

        var lines = new List<PackageQuoteLine>();
        var attributes = new List<PackageAttribute>();
        var normalized = new Dictionary<string, string>();
        var added = Money.Zero(package.Currency);

        foreach (var component in package.Components)
        {
            var total = ValueOf(component, chosen);
            var units = component.DeliveredUnits(total) - HeldUnits(component, held);

            DomainGuard.IsTrue(units < 0, Errors.PackageAdditionIsInvalid.With(component.Key));

            var unitPrice = UnitPriceOf(component, total);
            var amount = unitPrice * units;

            lines.Add(new PackageQuoteLine(component.Key, Text(component, total), units, unitPrice, amount));
            attributes.Add(new PackageAttribute(component.Key, Granted(component, total), component.Combination));

            if (component.EnableKey is not null)
            {
                attributes.Add(new PackageAttribute(component.EnableKey, Bool(total > 0), AttributeCombination.Or));
            }

            normalized[component.Key] = Text(component, total);
            added += amount;
        }

        DomainGuard.IsTrue(lines.All(x => x.Units == 0), Errors.PackageAdditionIsInvalid.With(package.Name));

        var (subTotal, tax, grandTotal) = package.WithTax(added);

        return new PackageQuote(package, normalized, lines, Money.Zero(package.Currency), false, subTotal, tax, grandTotal, attributes) { IsAddition = true };
    }

    private static long HeldUnits(PackageComponent component, IReadOnlyCollection<PackageAttribute> held)
    {
        var attribute = (held ?? []).FirstOrDefault(x => x.Key == component.Key);

        return attribute is not null && long.TryParse(attribute.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var units) ? units : 0;
    }

    private static long ValueOf(PackageComponent component, IReadOnlyDictionary<string, string> chosen)
    {
        if (!chosen.TryGetValue(component.Key, out var text) || string.IsNullOrWhiteSpace(text))
        {
            return component.Default;
        }

        text = text.Trim();

        if (component.Kind == PackageComponentKind.Toggle)
        {
            DomainGuard.IsFalse(bool.TryParse(text, out var on), Errors.PackageSelectionIsInvalid.With(component.Key));
            DomainGuard.IsTrue(component.Required && !on, Errors.PackageRequiredComponentIsOff.With(component.Key));

            return on ? 1 : 0;
        }

        DomainGuard.IsFalse(long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value), Errors.PackageSelectionIsInvalid.With(component.Key));

        if (component.Kind is PackageComponentKind.Quantity or PackageComponentKind.Volume)
        {
            DomainGuard.IsTrue(value < component.Min || value > component.Max || (value - component.Min) % component.Step != 0, Errors.PackageSelectionIsInvalid.With(component.Key));
        }
        else
        {
            DomainGuard.IsTrue(component.Tiers.All(x => x.Value != value), Errors.PackageSelectionIsInvalid.With(component.Key));
        }

        return value;
    }

    private static long UnitsOf(PackageComponent component, Dictionary<string, long> values) => component.Kind switch
    {
        PackageComponentKind.Quantity => values[component.Key],
        PackageComponentKind.Volume => component.DeliveredUnits(values[component.Key]),
        _ => component.PerUnitOf is null ? 1 : values[component.PerUnitOf]
    };

    private static Money UnitPriceOf(PackageComponent component, long value) => component.Kind switch
    {
        PackageComponentKind.Tiered => component.Tiers.First(x => x.Value == value).Price,
        // Por debajo del primer tramo solo queda el cero, que no entrega nada: su precio es el del componente, cero.
        PackageComponentKind.Volume => component.VolumeTierOf(value)?.Price ?? component.UnitPrice,
        _ => component.UnitPrice
    };

    /// <summary>
    /// El valor del atributo: lo elegido, o lo entregado en una cantidad por tramos de volumen.
    /// </summary>
    private static string Granted(PackageComponent component, long value) => component.Kind == PackageComponentKind.Volume
        ? component.DeliveredUnits(value).ToString(CultureInfo.InvariantCulture)
        : Text(component, value);

    private static string Text(PackageComponent component, long value) => component.Kind == PackageComponentKind.Toggle ? Bool(value == 1) : value.ToString(CultureInfo.InvariantCulture);

    private static string Bool(bool value) => value ? "true" : "false";
}

/// <summary>
/// La cotización de un paquete (pendings/739).
/// </summary>
/// <param name="Package">El paquete del catálogo.</param>
/// <param name="Selections">Lo elegido, completo con los valores por defecto; vacío en un fijo.</param>
/// <param name="Lines">El aporte de cada componente; vacío en un fijo.</param>
/// <param name="BasePrice">El precio del fijo o la base del configurable.</param>
/// <param name="MinimumApplied">Si se cobró el mínimo porque lo elegido sumaba menos.</param>
/// <param name="SubTotal">La base sin impuestos.</param>
/// <param name="Tax">El impuesto.</param>
/// <param name="Total">El total.</param>
/// <param name="Attributes">Los atributos que aporta.</param>
public sealed record PackageQuote(PackageAggregate Package, Dictionary<string, string> Selections, List<PackageQuoteLine> Lines, Money BasePrice, bool MinimumApplied, Money SubTotal, Money Tax, Money Total, List<PackageAttribute> Attributes)
{
    /// <summary>
    /// Si cotiza lo que se suma a un paquete consumible (pendings/744): los importes y las unidades de las líneas son solo
    /// lo añadido; los atributos y lo elegido, el total.
    /// </summary>
    public bool IsAddition { get; init; }

    /// <summary>
    /// La línea de la orden con esta cotización, aún sin periodo.
    /// </summary>
    /// <param name="lineId">El identificador de la línea.</param>
    /// <param name="replacesLineId">La línea que reemplaza, en una ampliación o una renovación.</param>
    /// <returns>La línea.</returns>
    public PackageLine ToLine(Guid lineId, Guid? replacesLineId) => PackageLine.Create(
        lineId,
        Package.Id,
        Package.Name,
        Package.Kind,
        new Dictionary<string, string>(Selections),
        [.. Attributes],
        SubTotal,
        Tax,
        Total,
        Package.TaxTreatment,
        Package.TaxBasisPoints,
        Package.TaxLegalBasis,
        Package.InvoiceConcept,
        replacesLineId) with
    {
        AddsToReplaced = IsAddition && replacesLineId is not null ? true : null
    };
}

/// <summary>
/// El aporte de un componente al precio.
/// </summary>
/// <param name="Key">La clave del componente.</param>
/// <param name="Value">El valor elegido, como texto.</param>
/// <param name="Units">Las unidades por las que se multiplicó; al sumar a un consumible, solo las añadidas.</param>
/// <param name="UnitPrice">El precio de una unidad.</param>
/// <param name="Amount">El aporte.</param>
public sealed record PackageQuoteLine(string Key, string Value, long Units, Money UnitPrice, Money Amount);
