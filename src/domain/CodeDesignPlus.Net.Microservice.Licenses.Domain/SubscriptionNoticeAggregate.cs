using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

/// <summary>
/// La constancia de un aviso de vencimiento de la licencia de una copropiedad (pendings/736, regla 23).
/// </summary>
/// <remarks>
/// Su identificador sale de la copropiedad, del fin del periodo pagado y del día del aviso: el job que corre dos veces el
/// mismo día encuentra la constancia y no repite el aviso. Una renovación o un cambio de licencia mueven el fin del
/// periodo, así que los avisos del periodo anterior dejan de tocar y los del nuevo nacen con otra clave.
/// <para>
/// Se guarda antes de publicar y se marca publicada después: si el bus falla, el job siguiente la encuentra sin publicar y
/// lo intenta otra vez con el mismo identificador de evento, que es el que usan los consumidores para no repetir el aviso.
/// </para>
/// </remarks>
/// <param name="id">El identificador derivado de la copropiedad, el fin del periodo y el día del aviso.</param>
public class SubscriptionNoticeAggregate(Guid id) : AggregateRoot(id)
{
    private static readonly Guid Namespace = Guid.Parse("2d6b8f40-7a1e-4c93-8e55-0b9f3c7d1736");

    /// <summary>
    /// La licencia que vence.
    /// </summary>
    public Guid LicenseId { get; private set; }

    /// <summary>
    /// El nombre de la licencia que vence.
    /// </summary>
    public string LicenseName { get; private set; } = null!;

    /// <summary>
    /// El nombre de la copropiedad.
    /// </summary>
    public string TenantName { get; private set; } = null!;

    /// <summary>
    /// El fin del periodo pagado.
    /// </summary>
    public Instant PeriodEnd { get; private set; }

    /// <summary>
    /// Cuándo termina la gracia.
    /// </summary>
    public Instant GraceEndsAt { get; private set; }

    /// <summary>
    /// La zona horaria de la copropiedad, para que quien avisa diga las fechas en su calendario.
    /// </summary>
    public string TimeZone { get; private set; } = null!;

    /// <summary>
    /// La etapa del aviso.
    /// </summary>
    public ExpirationStage Stage { get; private set; }

    /// <summary>
    /// El día del aviso, en días que faltaban para el vencimiento.
    /// </summary>
    public int Day { get; private set; }

    /// <summary>
    /// Los días que faltaban cuando se emitió.
    /// </summary>
    public int DaysRemaining { get; private set; }

    /// <summary>
    /// Si el evento ya salió por el bus.
    /// </summary>
    public bool Published { get; private set; }

    /// <summary>
    /// Los paquetes vigentes de la copropiedad, que se suspenden con la licencia si no se renueva (pendings/739). No va al
    /// constructor (regla 31): las constancias guardadas antes leen nulo.
    /// </summary>
    public List<string>? PackageNames { get; private set; }

    /// <summary>
    /// El identificador de la constancia de un aviso.
    /// </summary>
    /// <param name="tenant">La copropiedad.</param>
    /// <param name="periodEnd">El fin del periodo pagado.</param>
    /// <param name="notice">El aviso.</param>
    /// <returns>El identificador.</returns>
    public static Guid IdFor(Guid tenant, Instant periodEnd, ExpirationNotice notice) =>
        DeterministicGuid.Create(Namespace, tenant.ToString(), periodEnd.ToUnixTimeTicks().ToString(System.Globalization.CultureInfo.InvariantCulture), notice.Stage.ToString(), notice.Day.ToString(System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>
    /// Emite el aviso de una suscripción.
    /// </summary>
    /// <param name="subscription">La suscripción.</param>
    /// <param name="notice">El aviso que le toca.</param>
    /// <param name="now">El instante actual.</param>
    /// <param name="graceDays">Los días de gracia.</param>
    /// <param name="createdBy">El sistema.</param>
    /// <param name="packageNames">Los paquetes vigentes de la copropiedad, que vencen o se suspenden con la licencia.</param>
    /// <returns>La constancia, con su evento.</returns>
    public static SubscriptionNoticeAggregate Create(SubscriptionTimeline subscription, ExpirationNotice notice, Instant now, int graceDays, Guid createdBy, List<string>? packageNames = null)
    {
        DomainGuard.IsNull(subscription, Errors.SubscriptionIsRequired);
        DomainGuard.IsNull(notice, Errors.SubscriptionIsRequired);

        var current = subscription.CurrentAt(now);
        var periodEnd = subscription.PaidThrough;

        var aggregate = new SubscriptionNoticeAggregate(IdFor(subscription.TenantId, periodEnd, notice))
        {
            Tenant = subscription.TenantId,
            LicenseId = current.License.Id,
            LicenseName = current.License.Name,
            TenantName = current.TenantDetail.Name,
            PeriodEnd = periodEnd,
            GraceEndsAt = subscription.GraceEndsAt(graceDays),
            TimeZone = subscription.Zone.Id,
            Stage = notice.Stage,
            Day = notice.Day,
            DaysRemaining = subscription.DaysRemaining(now),
            IsActive = true,
            CreatedAt = now,
            CreatedBy = createdBy,
            PackageNames = packageNames ?? []
        };

        aggregate.AddEvent(aggregate.ToEvent());

        return aggregate;
    }

    /// <summary>
    /// El evento del aviso. Lleva como identificador el de la constancia: publicarlo otra vez no repite el aviso.
    /// </summary>
    /// <returns>El evento.</returns>
    public SubscriptionExpiringDomainEvent ToEvent() =>
        new(Id, Tenant, TenantName, LicenseId, LicenseName, PeriodEnd, GraceEndsAt, TimeZone, Stage.ToString(), DaysRemaining, eventId: Id, occurredAt: CreatedAt) { PackageNames = PackageNames ?? [] };

    /// <summary>
    /// Marca el evento como publicado.
    /// </summary>
    /// <param name="now">El instante actual.</param>
    public void MarkPublished(Instant now)
    {
        Published = true;
        UpdatedAt = now;
    }
}
