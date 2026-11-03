using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Lo que no se prestó de una orden pagada cuando la copropiedad cambia de licencia: es lo que dice la nota crédito sobre
/// la factura de esa orden (pendings/738, paso 2b).
/// </summary>
/// <param name="OrderId">La orden cuya factura se acredita.</param>
/// <param name="From">Desde cuándo no se presta (el día del cambio, o el inicio del periodo si aún no había empezado).</param>
/// <param name="To">El fin del periodo de esa orden.</param>
/// <param name="UnusedDays">Los días del periodo que no se prestan, contados en la zona horaria de la copropiedad.</param>
/// <param name="PeriodDays">Los días del periodo de esa orden.</param>
/// <param name="Base">La base facturada de esa orden, sin impuestos.</param>
/// <param name="Amount">La base que se acredita: la base por los días no prestados, redondeada a favor del cliente.</param>
public sealed record CreditedPeriod(Guid OrderId, Instant From, Instant To, int UnusedDays, int PeriodDays, Money Base, Money Amount)
{
    // Los miembros que siguen no van al constructor (regla 31): las órdenes de cambio guardadas antes acreditaban solo la
    // base y no los traen; leen nulo y cero, y su total es la base que se cobró entonces.

    /// <summary>
    /// El IVA que se acredita: el de la factura de esa orden, en proporción a la base acreditada y con su tarifa. Nulo en
    /// los cambios guardados antes de acreditarlo.
    /// </summary>
    public Money? Tax { get; private set; }

    /// <summary>
    /// La tarifa del impuesto de la factura acreditada, en puntos básicos (1900 = 19 %); cero si no fue gravada.
    /// </summary>
    public int TaxBasisPoints { get; private set; }

    /// <summary>
    /// La línea de paquete que se acredita; nula si es la licencia (pendings/739). Nula también en los cambios guardados
    /// antes de existir los paquetes, que solo acreditaban la licencia.
    /// </summary>
    public Guid? LineId { get; private set; }

    /// <summary>
    /// Lo que se acredita en total: la base acreditada más su IVA.
    /// </summary>
    public Money Total => Tax is null ? Amount : Amount + Tax;

    /// <summary>
    /// Crea el periodo acreditado con la base y el IVA de lo no prestado.
    /// </summary>
    /// <param name="orderId">La orden cuya factura se acredita.</param>
    /// <param name="from">Desde cuándo no se presta.</param>
    /// <param name="to">El fin del periodo de esa orden.</param>
    /// <param name="unusedDays">Los días que no se prestan.</param>
    /// <param name="periodDays">Los días del periodo.</param>
    /// <param name="base">La base facturada de esa orden, sin impuestos.</param>
    /// <param name="amount">La base que se acredita.</param>
    /// <param name="tax">El IVA que se acredita.</param>
    /// <param name="taxBasisPoints">La tarifa de la factura acreditada.</param>
    /// <returns>El periodo acreditado.</returns>
    public static CreditedPeriod Create(Guid orderId, Instant from, Instant to, int unusedDays, int periodDays, Money @base, Money amount, Money tax, int taxBasisPoints)
    {
        return new CreditedPeriod(orderId, from, to, unusedDays, periodDays, @base, amount) { Tax = tax, TaxBasisPoints = taxBasisPoints };
    }

    /// <summary>
    /// El mismo periodo acreditado, de una línea de paquete.
    /// </summary>
    /// <param name="lineId">La línea.</param>
    /// <returns>El periodo acreditado de esa línea.</returns>
    public CreditedPeriod ForLine(Guid lineId) => this with { LineId = lineId };
}
