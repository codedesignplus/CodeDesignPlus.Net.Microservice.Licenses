using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.DataTransferObjects;

/// <summary>
/// Una licencia a la que la copropiedad se puede cambiar, con su cotización (pendings/738).
/// </summary>
/// <remarks>
/// Los importes van en unidades mínimas de <see cref="Currency"/> (regla 04). <see cref="Price"/> es el precio completo
/// con impuestos, que es el que se cobra y el que dice la factura; <see cref="Credit"/> y <see cref="CreditBalanceApplied"/>
/// lo pagan en parte como anticipo, y <see cref="Total"/> es lo que queda por pagar en la pasarela. Si el crédito supera el
/// precio, <see cref="Total"/> es cero y el sobrante queda como saldo a favor (<see cref="RemainingCreditBalance"/>).
/// </remarks>
public class ChangeOptionDto
{
    /// <summary>
    /// La licencia del catálogo.
    /// </summary>
    public Guid LicenseId { get; set; }

    /// <summary>
    /// Su nombre.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Su descripción corta.
    /// </summary>
    public string ShortDescription { get; set; } = string.Empty;

    /// <summary>
    /// Su descripción.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Su icono.
    /// </summary>
    public Icon Icon { get; set; } = null!;

    /// <summary>
    /// Si es la licencia destacada.
    /// </summary>
    public bool IsPopular { get; set; }

    /// <summary>
    /// Si es una licencia a medida: privada (no se ve en la landing) y ofrecida a esta copropiedad (pendings/464).
    /// </summary>
    public bool IsTailored { get; set; }

    /// <summary>
    /// La periodicidad: la misma de la copropiedad.
    /// </summary>
    public BillingType BillingType { get; set; }

    /// <summary>
    /// La moneda de los importes.
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// El precio completo del periodo nuevo, con impuestos.
    /// </summary>
    public long Price { get; set; }

    /// <summary>
    /// La base del precio, sin impuestos.
    /// </summary>
    public long PriceSubTotal { get; set; }

    /// <summary>
    /// Los impuestos del precio.
    /// </summary>
    public long PriceTax { get; set; }

    /// <summary>
    /// Cómo trata el impuesto el precio: gravado, excluido o exento (pendings/662).
    /// </summary>
    public TaxTreatment TaxTreatment { get; set; }

    /// <summary>
    /// La tarifa del impuesto en puntos básicos; cero si no es gravado.
    /// </summary>
    public int TaxBasisPoints { get; set; }

    /// <summary>
    /// La base legal de un precio excluido o exento.
    /// </summary>
    public string? TaxLegalBasis { get; set; }

    /// <summary>
    /// El crédito por lo no prestado de la licencia actual, con su IVA: la base no prestada más el IVA que se pagó por
    /// ella (pendings/738).
    /// </summary>
    public long Credit { get; set; }

    /// <summary>
    /// El IVA incluido en <see cref="Credit"/>.
    /// </summary>
    public long CreditTax { get; set; }

    /// <summary>
    /// La parte del crédito que paga este precio (el crédito, hasta el precio).
    /// </summary>
    public long CreditApplied { get; set; }

    /// <summary>
    /// La parte del saldo a favor que paga este precio.
    /// </summary>
    public long CreditBalanceApplied { get; set; }

    /// <summary>
    /// Lo que queda por pagar en la pasarela; cero si el crédito y el saldo a favor lo cubren.
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// El saldo a favor que quedará después del cambio.
    /// </summary>
    public long RemainingCreditBalance { get; set; }

    /// <summary>
    /// El detalle del crédito: lo no prestado de cada periodo pagado.
    /// </summary>
    public List<CreditedPeriodDto> CreditDetail { get; set; } = [];

    /// <summary>
    /// Los módulos que gana.
    /// </summary>
    public List<ModuleDto> GainedModules { get; set; } = [];

    /// <summary>
    /// Los atributos que gana o que suben, con el valor de la licencia nueva.
    /// </summary>
    public Dictionary<string, string> GainedAttributes { get; set; } = [];

    /// <summary>
    /// Todos sus módulos.
    /// </summary>
    public List<ModuleDto> Modules { get; set; } = [];

    /// <summary>
    /// Todos sus atributos.
    /// </summary>
    public Dictionary<string, string> Attributes { get; set; } = [];
}

/// <summary>
/// Lo no prestado de un periodo pagado (pendings/738).
/// </summary>
public class CreditedPeriodDto
{
    /// <summary>
    /// La orden de ese periodo.
    /// </summary>
    public Guid OrderId { get; set; }

    /// <summary>
    /// Desde cuándo no se presta.
    /// </summary>
    public Instant From { get; set; }

    /// <summary>
    /// El fin de ese periodo.
    /// </summary>
    public Instant To { get; set; }

    /// <summary>
    /// Los días que no se prestan.
    /// </summary>
    public int UnusedDays { get; set; }

    /// <summary>
    /// Los días del periodo.
    /// </summary>
    public int PeriodDays { get; set; }

    /// <summary>
    /// La base facturada de ese periodo, sin impuestos.
    /// </summary>
    public long Base { get; set; }

    /// <summary>
    /// La base que se acredita, sin impuestos.
    /// </summary>
    public long Amount { get; set; }

    /// <summary>
    /// El IVA que se acredita, con la tarifa de la factura de ese periodo.
    /// </summary>
    public long Tax { get; set; }

    /// <summary>
    /// La tarifa de esa factura, en puntos básicos; cero si no fue gravada.
    /// </summary>
    public int TaxBasisPoints { get; set; }

    /// <summary>
    /// Lo que se acredita en total: la base más su IVA.
    /// </summary>
    public long Total { get; set; }
}
