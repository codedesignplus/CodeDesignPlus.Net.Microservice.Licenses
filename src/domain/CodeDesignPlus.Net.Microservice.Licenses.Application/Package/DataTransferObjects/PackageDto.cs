using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;

/// <summary>
/// Un paquete complementario del catálogo (pendings/739). Lectura: importes en unidades mínimas de <see cref="Currency"/>
/// y la tarifa en puntos básicos (reglas 04 y 05).
/// </summary>
public class PackageDto : IDtoBase
{
    /// <summary>
    /// El identificador.
    /// </summary>
    public Guid Id { get; set; }

    /// <summary>
    /// El nombre.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// La descripción corta.
    /// </summary>
    public string ShortDescription { get; set; } = string.Empty;

    /// <summary>
    /// La descripción.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// El ícono.
    /// </summary>
    public Icon Icon { get; set; } = null!;

    /// <summary>
    /// Si está activo.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// Si se destaca.
    /// </summary>
    public bool IsPopular { get; set; }

    /// <summary>
    /// Si es público.
    /// </summary>
    public bool ShowInLandingPage { get; set; }

    /// <summary>
    /// Las copropiedades a las que se ofrece si es privado.
    /// </summary>
    public List<LicenseOfferDto> OfferedTo { get; set; } = [];

    /// <summary>
    /// Fijo o configurable.
    /// </summary>
    public PackageKind Kind { get; set; }

    /// <summary>
    /// La moneda.
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// El precio del fijo, o la base del configurable.
    /// </summary>
    public long BasePrice { get; set; }

    /// <summary>
    /// El piso del configurable.
    /// </summary>
    public long MinimumPrice { get; set; }

    /// <summary>
    /// El tratamiento del impuesto.
    /// </summary>
    public TaxTreatment TaxTreatment { get; set; }

    /// <summary>
    /// La tarifa en puntos básicos.
    /// </summary>
    public int TaxBasisPoints { get; set; }

    /// <summary>
    /// La base legal de un excluido o exento.
    /// </summary>
    public string? TaxLegalBasis { get; set; }

    /// <summary>
    /// El concepto de la factura.
    /// </summary>
    public string InvoiceConcept { get; set; } = string.Empty;

    /// <summary>
    /// Los atributos del fijo.
    /// </summary>
    public List<PackageAttributeDto> Attributes { get; set; } = [];

    /// <summary>
    /// Los componentes del configurable.
    /// </summary>
    public List<PackageComponentDto> Components { get; set; } = [];

    /// <summary>
    /// El precio del fijo, o el del configurable con sus valores por defecto.
    /// </summary>
    public PackagePriceDto Price { get; set; } = new();

    /// <summary>
    /// El paquete del catálogo para la pantalla.
    /// </summary>
    /// <param name="package">El paquete.</param>
    /// <returns>El DTO.</returns>
    public static PackageDto From(PackageAggregate package) => new()
    {
        Id = package.Id,
        Name = package.Name,
        ShortDescription = package.ShortDescription,
        Description = package.Description,
        Icon = package.Icon,
        IsActive = package.IsActive,
        IsPopular = package.IsPopular,
        ShowInLandingPage = package.ShowInLandingPage,
        OfferedTo = [.. package.OfferedTo.Select(x => new LicenseOfferDto { TenantId = x.TenantId, TenantName = x.TenantName, OfferedBy = x.OfferedBy, OfferedAt = x.OfferedAt })],
        Kind = package.Kind,
        Currency = package.Currency,
        BasePrice = package.BasePrice.Amount,
        MinimumPrice = package.MinimumPrice.Amount,
        TaxTreatment = package.TaxTreatment,
        TaxBasisPoints = package.TaxBasisPoints,
        TaxLegalBasis = package.TaxLegalBasis,
        InvoiceConcept = package.InvoiceConcept,
        Attributes = [.. package.Attributes.Select(PackageAttributeDto.From)],
        Components = [.. package.Components.Select(PackageComponentDto.From)],
        Price = DefaultPrice(package)
    };

    private static PackagePriceDto DefaultPrice(PackageAggregate package)
    {
        try
        {
            var quote = PackagePricing.Quote(package, null);

            return new PackagePriceDto { SubTotal = quote.SubTotal.Amount, Tax = quote.Tax.Amount, Total = quote.Total.Amount };
        }
        catch (CodeDesignPlus.Net.Exceptions.CodeDesignPlusException)
        {
            // Un configurable cuyos valores por defecto no cobran nada (todo en cero, sin base ni mínimo): «desde $0».
            return new PackagePriceDto();
        }
    }
}

/// <summary>
/// Un atributo de un paquete.
/// </summary>
public class PackageAttributeDto
{
    /// <summary>
    /// La clave.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// El valor como texto.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Cómo se combina con el de la licencia.
    /// </summary>
    public AttributeCombination Combine { get; set; }

    /// <summary>
    /// El atributo para la pantalla.
    /// </summary>
    /// <param name="attribute">El atributo.</param>
    /// <returns>El DTO.</returns>
    public static PackageAttributeDto From(PackageAttribute attribute) => new() { Key = attribute.Key, Value = attribute.Value, Combine = attribute.Combination };
}

/// <summary>
/// Un componente de un paquete configurable.
/// </summary>
public class PackageComponentDto
{
    /// <summary>
    /// La clave del atributo.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Cantidad, tramos o sí/no.
    /// </summary>
    public PackageComponentKind Kind { get; set; }

    /// <summary>
    /// Cómo se combina.
    /// </summary>
    public AttributeCombination Combine { get; set; }

    /// <summary>
    /// El mínimo de una cantidad.
    /// </summary>
    public long Min { get; set; }

    /// <summary>
    /// El máximo de una cantidad.
    /// </summary>
    public long Max { get; set; }

    /// <summary>
    /// El paso de una cantidad.
    /// </summary>
    public long Step { get; set; }

    /// <summary>
    /// El valor por defecto.
    /// </summary>
    public long Default { get; set; }

    /// <summary>
    /// El precio de una unidad o del sí, en unidades mínimas.
    /// </summary>
    public long UnitPrice { get; set; }

    /// <summary>
    /// La cantidad por cuyas unidades se multiplica el precio.
    /// </summary>
    public string? PerUnitOf { get; set; }

    /// <summary>
    /// El sí/no que enciende una cantidad mayor que cero.
    /// </summary>
    public string? EnableKey { get; set; }

    /// <summary>
    /// Si el sí/no va siempre encendido.
    /// </summary>
    public bool Required { get; set; }

    /// <summary>
    /// Los tramos.
    /// </summary>
    public List<PackageTierDto> Tiers { get; set; } = [];

    /// <summary>
    /// El componente para la pantalla.
    /// </summary>
    /// <param name="component">El componente.</param>
    /// <returns>El DTO.</returns>
    public static PackageComponentDto From(PackageComponent component) => new()
    {
        Key = component.Key,
        Kind = component.Kind,
        Combine = component.Combination,
        Min = component.Min,
        Max = component.Max,
        Step = component.Step,
        Default = component.Default,
        UnitPrice = component.UnitPrice.Amount,
        PerUnitOf = component.PerUnitOf,
        EnableKey = component.EnableKey,
        Required = component.Required,
        Tiers = [.. (component.Tiers ?? []).Select(x => new PackageTierDto { Value = x.Value, Price = x.Price.Amount })]
    };
}

/// <summary>
/// Un tramo.
/// </summary>
public class PackageTierDto
{
    /// <summary>
    /// El valor del tramo.
    /// </summary>
    public long Value { get; set; }

    /// <summary>
    /// Su precio en unidades mínimas.
    /// </summary>
    public long Price { get; set; }
}

/// <summary>
/// Un precio con su impuesto, en unidades mínimas.
/// </summary>
public class PackagePriceDto
{
    /// <summary>
    /// La base.
    /// </summary>
    public long SubTotal { get; set; }

    /// <summary>
    /// El impuesto.
    /// </summary>
    public long Tax { get; set; }

    /// <summary>
    /// El total.
    /// </summary>
    public long Total { get; set; }
}
