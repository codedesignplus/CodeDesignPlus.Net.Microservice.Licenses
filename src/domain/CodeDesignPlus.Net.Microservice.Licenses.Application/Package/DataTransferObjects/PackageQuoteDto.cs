using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;

/// <summary>
/// La cotización de un paquete, calculada siempre por ms-licenses (pendings/739, regla 06). Importes en unidades mínimas.
/// </summary>
public class PackageQuoteDto
{
    /// <summary>
    /// El paquete.
    /// </summary>
    public Guid PackageId { get; set; }

    /// <summary>
    /// Su nombre.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Fijo o configurable.
    /// </summary>
    public PackageKind Kind { get; set; }

    /// <summary>
    /// La moneda.
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// Lo elegido, completo con los valores por defecto.
    /// </summary>
    public Dictionary<string, string> Selections { get; set; } = [];

    /// <summary>
    /// El aporte de cada componente.
    /// </summary>
    public List<PackageQuoteLineDto> Lines { get; set; } = [];

    /// <summary>
    /// El precio del fijo o la base del configurable.
    /// </summary>
    public long BasePrice { get; set; }

    /// <summary>
    /// Si se cobró el mínimo.
    /// </summary>
    public bool MinimumApplied { get; set; }

    /// <summary>
    /// La base sin impuestos.
    /// </summary>
    public long SubTotal { get; set; }

    /// <summary>
    /// El tratamiento del impuesto.
    /// </summary>
    public TaxTreatment TaxTreatment { get; set; }

    /// <summary>
    /// La tarifa en puntos básicos.
    /// </summary>
    public int TaxBasisPoints { get; set; }

    /// <summary>
    /// El impuesto.
    /// </summary>
    public long Tax { get; set; }

    /// <summary>
    /// El total.
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// Los atributos que aporta.
    /// </summary>
    public List<PackageAttributeDto> Attributes { get; set; } = [];

    /// <summary>
    /// En una ampliación, la línea que reemplaza.
    /// </summary>
    public Guid? ReplacesLineId { get; set; }

    /// <summary>
    /// Lo no usado de la línea reemplazada, con su IVA.
    /// </summary>
    public long Credit { get; set; }

    /// <summary>
    /// El IVA de ese crédito.
    /// </summary>
    public long CreditTax { get; set; }

    /// <summary>
    /// El saldo a favor que se aplica.
    /// </summary>
    public long CreditBalanceApplied { get; set; }

    /// <summary>
    /// Lo que se cobra por la pasarela.
    /// </summary>
    public long AmountDue { get; set; }

    /// <summary>
    /// El saldo a favor que queda después.
    /// </summary>
    public long RemainingCreditBalance { get; set; }

    /// <summary>
    /// Desde cuándo cuenta, si se cotiza para una copropiedad.
    /// </summary>
    public Instant? PeriodStart { get; set; }

    /// <summary>
    /// Hasta cuándo cuenta, si se cotiza para una copropiedad.
    /// </summary>
    public Instant? PeriodEnd { get; set; }

    /// <summary>
    /// La cotización sin copropiedad: se cobra entera.
    /// </summary>
    /// <param name="quote">La cotización.</param>
    /// <returns>El DTO.</returns>
    public static PackageQuoteDto From(PackageQuote quote) => new()
    {
        PackageId = quote.Package.Id,
        Name = quote.Package.Name,
        Kind = quote.Package.Kind,
        Currency = quote.Total.Currency,
        Selections = new Dictionary<string, string>(quote.Selections),
        Lines = [.. quote.Lines.Select(x => new PackageQuoteLineDto { Key = x.Key, Value = x.Value, Units = x.Units, UnitPrice = x.UnitPrice.Amount, Amount = x.Amount.Amount })],
        BasePrice = quote.BasePrice.Amount,
        MinimumApplied = quote.MinimumApplied,
        SubTotal = quote.SubTotal.Amount,
        TaxTreatment = quote.Package.TaxTreatment,
        TaxBasisPoints = quote.Package.TaxBasisPoints,
        Tax = quote.Tax.Amount,
        Total = quote.Total.Amount,
        Attributes = [.. quote.Attributes.Select(PackageAttributeDto.From)],
        AmountDue = quote.Total.Amount
    };
}

/// <summary>
/// El aporte de un componente.
/// </summary>
public class PackageQuoteLineDto
{
    /// <summary>
    /// La clave.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// El valor elegido.
    /// </summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>
    /// Las unidades por las que se multiplicó.
    /// </summary>
    public long Units { get; set; }

    /// <summary>
    /// El precio de una unidad.
    /// </summary>
    public long UnitPrice { get; set; }

    /// <summary>
    /// El aporte.
    /// </summary>
    public long Amount { get; set; }
}

/// <summary>
/// Un paquete de una orden o de la suscripción (pendings/739). Importes en unidades mínimas.
/// </summary>
public class PackageLineDto
{
    /// <summary>
    /// La línea.
    /// </summary>
    public Guid LineId { get; set; }

    /// <summary>
    /// La orden que la pagó.
    /// </summary>
    public Guid? OrderId { get; set; }

    /// <summary>
    /// El paquete del catálogo.
    /// </summary>
    public Guid PackageId { get; set; }

    /// <summary>
    /// Su nombre al comprarlo.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Fijo o configurable.
    /// </summary>
    public PackageKind Kind { get; set; }

    /// <summary>
    /// Lo elegido.
    /// </summary>
    public Dictionary<string, string> Selections { get; set; } = [];

    /// <summary>
    /// Los atributos que aporta.
    /// </summary>
    public List<PackageAttributeDto> Attributes { get; set; } = [];

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

    /// <summary>
    /// La moneda.
    /// </summary>
    public string Currency { get; set; } = string.Empty;

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
    public string? InvoiceConcept { get; set; }

    /// <summary>
    /// El inicio de su periodo; nulo mientras no se pague.
    /// </summary>
    public Instant? PeriodStart { get; set; }

    /// <summary>
    /// El fin de su periodo; nulo mientras no se pague.
    /// </summary>
    public Instant? PeriodEnd { get; set; }

    /// <summary>
    /// La línea que reemplaza.
    /// </summary>
    public Guid? ReplacesLineId { get; set; }

    /// <summary>
    /// Si cuenta hoy: la licencia pagada y la línea en su periodo, sin otra que la reemplace.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// La línea para la pantalla.
    /// </summary>
    /// <param name="line">La línea.</param>
    /// <param name="orderId">La orden que la pagó.</param>
    /// <param name="isActive">Si cuenta hoy.</param>
    /// <returns>El DTO.</returns>
    public static PackageLineDto From(PackageLine line, Guid? orderId = null, bool isActive = false) => new()
    {
        LineId = line.LineId,
        OrderId = orderId,
        PackageId = line.PackageId,
        Name = line.Name,
        Kind = line.Kind,
        Selections = new Dictionary<string, string>(line.Selections ?? []),
        Attributes = [.. line.Attributes.Select(PackageAttributeDto.From)],
        SubTotal = line.SubTotal.Amount,
        Tax = line.Tax.Amount,
        Total = line.Total.Amount,
        Currency = line.Total.Currency,
        TaxTreatment = line.TaxTreatment,
        TaxBasisPoints = line.TaxBasisPoints,
        TaxLegalBasis = line.TaxLegalBasis,
        InvoiceConcept = line.InvoiceConcept,
        PeriodStart = line.PeriodStart,
        PeriodEnd = line.PeriodEnd,
        ReplacesLineId = line.ReplacesLineId,
        IsActive = isActive
    };
}
