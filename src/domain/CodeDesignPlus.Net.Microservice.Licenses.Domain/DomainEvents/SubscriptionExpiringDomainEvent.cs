namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;

/// <summary>
/// A la licencia de una copropiedad le toca un aviso de vencimiento (pendings/736).
/// </summary>
/// <remarks>
/// ms-licenses no sabe a quién avisar: es de CodeDesignPlus y no conoce los cargos de Kappali. Quien consume el evento
/// resuelve los destinatarios. Su identificador es el de la constancia del aviso: si llega dos veces, es el mismo aviso.
/// <para>
/// La etapa viaja como texto (<c>Upcoming</c>, <c>Expired</c>, <c>Blocked</c>) y no como número: un número cambia de
/// significado en cuanto alguien reordena el enum en un solo micro (regla 26, §4).
/// </para>
/// </remarks>
/// <param name="aggregateId">La constancia del aviso.</param>
/// <param name="tenant">La copropiedad.</param>
/// <param name="tenantName">El nombre de la copropiedad.</param>
/// <param name="licenseId">La licencia que vence.</param>
/// <param name="licenseName">El nombre de la licencia.</param>
/// <param name="endDate">Hasta cuándo está pagada.</param>
/// <param name="graceEndsAt">Cuándo termina la gracia.</param>
/// <param name="timeZone">La zona horaria de la copropiedad.</param>
/// <param name="stage">La etapa: <c>Upcoming</c>, <c>Expired</c> o <c>Blocked</c>.</param>
/// <param name="daysRemaining">Los días de calendario que faltan: cero el día que vence, negativos después.</param>
/// <param name="eventId">El identificador del evento.</param>
/// <param name="occurredAt">Cuándo ocurrió.</param>
/// <param name="metadata">Los metadatos.</param>
[EventKey<SubscriptionNoticeAggregate>(1, "SubscriptionExpiringDomainEvent")]
public class SubscriptionExpiringDomainEvent(
    Guid aggregateId,
    Guid tenant,
    string tenantName,
    Guid licenseId,
    string licenseName,
    Instant endDate,
    Instant graceEndsAt,
    string timeZone,
    string stage,
    int daysRemaining,
    Guid? eventId = null,
    Instant? occurredAt = null,
    Dictionary<string, object>? metadata = null
) : DomainEvent(aggregateId, eventId, occurredAt, metadata)
{
    /// <summary>
    /// La copropiedad.
    /// </summary>
    public Guid Tenant { get; } = tenant;

    /// <summary>
    /// El nombre de la copropiedad.
    /// </summary>
    public string TenantName { get; } = tenantName;

    /// <summary>
    /// La licencia que vence.
    /// </summary>
    public Guid LicenseId { get; } = licenseId;

    /// <summary>
    /// El nombre de la licencia.
    /// </summary>
    public string LicenseName { get; } = licenseName;

    /// <summary>
    /// Hasta cuándo está pagada.
    /// </summary>
    public Instant EndDate { get; } = endDate;

    /// <summary>
    /// Cuándo termina la gracia.
    /// </summary>
    public Instant GraceEndsAt { get; } = graceEndsAt;

    /// <summary>
    /// La zona horaria de la copropiedad.
    /// </summary>
    public string TimeZone { get; } = timeZone;

    /// <summary>
    /// La etapa del aviso.
    /// </summary>
    public string Stage { get; } = stage;

    /// <summary>
    /// Los días de calendario que faltan.
    /// </summary>
    public int DaysRemaining { get; } = daysRemaining;

    /// <summary>
    /// Los paquetes vigentes de la copropiedad: dejan de contar si la licencia no se renueva (pendings/739). Vacío si no tiene.
    /// </summary>
    public List<string> PackageNames { get; init; } = [];
}
