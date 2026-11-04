using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using LicenseStanding = CodeDesignPlus.Net.Security.Abstractions.Models.LicenseStanding;
using LicenseStatus = CodeDesignPlus.Net.Security.Abstractions.Models.LicenseStatus;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;

/// <summary>
/// La suscripción de una copropiedad: sus órdenes pagadas y vigentes puestas en línea de tiempo (pendings/735).
/// </summary>
/// <remarks>
/// Una copropiedad puede tener varias órdenes pagadas a la vez: la del periodo en curso y renovaciones pagadas por
/// adelantado. La suscripción está pagada hasta el fin de la última, y su licencia es la de la orden en curso. Las fechas
/// de calendario (los días que faltan, el día del vencimiento) se cuentan en la zona horaria de la copropiedad.
/// <para>
/// Solo cuentan las órdenes activas, pagadas y con periodo: un cambio de licencia inactiva las anteriores, y una orden sin
/// periodo no paga nada que se pueda contar.
/// </para>
/// </remarks>
public sealed class SubscriptionTimeline
{
    private SubscriptionTimeline(List<OrderAggregate> orders, DateTimeZone zone)
    {
        Orders = orders;
        Zone = zone;
    }

    /// <summary>
    /// Las órdenes que pagan la suscripción, por inicio de su periodo.
    /// </summary>
    public IReadOnlyList<OrderAggregate> Orders { get; }

    /// <summary>
    /// La zona horaria de la copropiedad.
    /// </summary>
    public DateTimeZone Zone { get; }

    /// <summary>
    /// La copropiedad.
    /// </summary>
    public Guid TenantId => Orders[0].TenantDetail.Id;

    /// <summary>
    /// Hasta cuándo está pagada: el fin del periodo de la última orden.
    /// </summary>
    public Instant PaidThrough => Orders.Max(x => x.PeriodEnd!.Value);

    /// <summary>
    /// Desde cuándo está pagada sin interrupción hasta <see cref="PaidThrough"/>.
    /// </summary>
    public Instant CoverageStart
    {
        get
        {
            var start = Orders[^1].PeriodStart!.Value;

            for (var i = Orders.Count - 2; i >= 0; i--)
            {
                if (Orders[i].PeriodEnd!.Value < start)
                {
                    break;
                }

                start = Instant.Min(start, Orders[i].PeriodStart!.Value);
            }

            return start;
        }
    }

    /// <summary>
    /// El día del vencimiento en la zona de la copropiedad.
    /// </summary>
    public LocalDate EndDate => PaidThrough.InZone(Zone).Date;

    /// <summary>
    /// Arma la suscripción con las órdenes de una copropiedad.
    /// </summary>
    /// <param name="orders">Las órdenes de la copropiedad; se descartan las que no cuentan.</param>
    /// <returns>La suscripción, o <c>null</c> si ninguna orden paga un periodo.</returns>
    public static SubscriptionTimeline? From(IEnumerable<OrderAggregate> orders)
    {
        var paying = orders
            // Una orden de paquete no paga la licencia: su periodo es el del paquete (pendings/739).
            .Where(x => x.IsActive && x.Type != OrderType.Package && x.PaymentStatus == PaymentStatus.Succeeded && x.PeriodStart is not null && x.PeriodEnd is not null)
            .OrderBy(x => x.PeriodStart)
            .ThenBy(x => x.CreatedAt)
            .ToList();

        if (paying.Count == 0)
        {
            return null;
        }

        return new SubscriptionTimeline(paying, ZoneOf(paying[^1].TenantDetail));
    }

    /// <summary>
    /// La zona horaria de una copropiedad: la de su ciudad o, si no la tiene, la de su país.
    /// </summary>
    /// <param name="tenant">La copropiedad.</param>
    /// <returns>La zona; UTC si ninguna es reconocible.</returns>
    public static DateTimeZone ZoneOf(Tenant tenant)
    {
        var city = tenant.Location?.City?.Timezone;
        var id = string.IsNullOrWhiteSpace(city) ? tenant.Location?.Country?.Timezone : city;

        return (string.IsNullOrWhiteSpace(id) ? null : DateTimeZoneProviders.Tzdb.GetZoneOrNull(id)) ?? DateTimeZone.Utc;
    }

    /// <summary>
    /// La orden en curso: la última que ya empezó; si todas son futuras, la primera.
    /// </summary>
    /// <param name="now">El instante actual.</param>
    /// <returns>La orden.</returns>
    public OrderAggregate CurrentAt(Instant now) => Orders.LastOrDefault(x => x.PeriodStart!.Value <= now) ?? Orders[0];

    /// <summary>
    /// El día de hoy en la zona de la copropiedad.
    /// </summary>
    /// <param name="now">El instante actual.</param>
    /// <returns>La fecha.</returns>
    public LocalDate Today(Instant now) => now.InZone(Zone).Date;

    /// <summary>
    /// Los días de calendario que faltan para el vencimiento: cero el mismo día, negativos después.
    /// </summary>
    /// <param name="now">El instante actual.</param>
    /// <returns>Los días.</returns>
    public int DaysRemaining(Instant now) => Period.DaysBetween(Today(now), EndDate);

    /// <summary>
    /// En qué punto de su licencia está, con el mismo cálculo que el middleware de licencias del SDK (pendings/737): activa
    /// hasta el fin de lo pagado, en gracia hasta los días de gracia después y en solo lectura luego.
    /// </summary>
    /// <remarks>
    /// Lo decide <see cref="LicenseStanding.Evaluate"/>, el mismo que usa Security con <c>Security:GraceDays</c>: si el aviso
    /// o la pantalla calcularan la gracia por su lado, dirían «te quedan 3 días» cuando la API ya no deja escribir. El fin de
    /// lo pagado es <see cref="PaidThrough"/>, que es la fecha de fin de la licencia que ms-tenants pone en el tenant.
    /// </remarks>
    /// <param name="now">El instante actual.</param>
    /// <param name="graceDays">Los días de gracia, los de <c>Security:GraceDays</c>.</param>
    /// <returns>El estado de la licencia.</returns>
    public LicenseStanding Standing(Instant now, int graceDays) => LicenseStanding.Evaluate(PaidThrough, now, graceDays);

    /// <summary>
    /// Cuándo termina la gracia y la licencia queda en solo lectura (pendings/737).
    /// </summary>
    /// <param name="graceDays">Los días de gracia, los de <c>Security:GraceDays</c>.</param>
    /// <returns>El instante.</returns>
    public Instant GraceEndsAt(int graceDays) => Standing(PaidThrough, graceDays).GraceEndsAt;

    /// <summary>
    /// Los días de calendario desde el día en que terminó la gracia, en la zona de la copropiedad: cero ese mismo día,
    /// negativos antes.
    /// </summary>
    /// <param name="now">El instante actual.</param>
    /// <param name="graceDays">Los días de gracia, los de <c>Security:GraceDays</c>.</param>
    /// <returns>Los días.</returns>
    public int DaysSinceGraceEnded(Instant now, int graceDays) => Period.DaysBetween(GraceEndsAt(graceDays).InZone(Zone).Date, Today(now));

    /// <summary>
    /// En qué punto de su vigencia está.
    /// </summary>
    /// <param name="now">El instante actual.</param>
    /// <param name="expiringWindowDays">Desde cuántos días antes del vencimiento se considera por vencer.</param>
    /// <param name="graceDays">Los días de gracia, los de <c>Security:GraceDays</c>.</param>
    /// <returns>El estado.</returns>
    public SubscriptionStatus StatusAt(Instant now, int expiringWindowDays, int graceDays) => Standing(now, graceDays).Status switch
    {
        LicenseStatus.ReadOnly => SubscriptionStatus.Expired,
        LicenseStatus.Grace => SubscriptionStatus.Grace,
        _ => DaysRemaining(now) <= expiringWindowDays ? SubscriptionStatus.Expiring : SubscriptionStatus.Active,
    };

    /// <summary>
    /// La licencia de la copropiedad como la aplica ms-tenants: la de la orden en curso, vigente desde
    /// <see cref="CoverageStart"/> hasta <see cref="PaidThrough"/>.
    /// </summary>
    /// <param name="now">El instante actual.</param>
    /// <returns>La licencia.</returns>
    public LicenseTenant ToLicenseTenant(Instant now)
    {
        var license = CurrentAt(now).License;

        return LicenseTenant.Create(license.Id, license.Name, CoverageStart, PaidThrough, license.Modules, license.Attributes);
    }
}
