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
/// <param name="Amount">Lo que se acredita: la base por los días no prestados, redondeado a favor del cliente.</param>
public sealed record CreditedPeriod(Guid OrderId, Instant From, Instant To, int UnusedDays, int PeriodDays, Money Base, Money Amount);
