using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

/// <summary>
/// La suscripción de una copropiedad: la licencia de la orden en curso, hasta cuándo está pagada y en qué punto de su
/// vigencia está (pendings/735).
/// </summary>
/// <remarks>
/// Los importes van en unidades mínimas de <see cref="Currency"/>: el cliente convierte para mostrar (regla 04).
/// </remarks>
public class SubscriptionDto
{
    /// <summary>
    /// La orden en curso.
    /// </summary>
    public required Guid Id { get; set; }

    /// <summary>
    /// La copia de la licencia de la orden en curso.
    /// </summary>
    public SubscriptionLicenseDto License { get; set; } = null!;

    /// <summary>
    /// El estado del pago de la orden en curso.
    /// </summary>
    public PaymentStatus PaymentStatus { get; set; }

    /// <summary>
    /// El estado del aprovisionamiento de la orden en curso.
    /// </summary>
    public ProvisioningStatus ProvisioningStatus { get; set; }

    /// <summary>
    /// Cuándo se creó la orden en curso.
    /// </summary>
    public Instant CreatedAt { get; set; }

    /// <summary>
    /// Si la orden en curso está activa.
    /// </summary>
    public bool IsActive { get; set; }

    /// <summary>
    /// El inicio del periodo en curso.
    /// </summary>
    public Instant PeriodStart { get; set; }

    /// <summary>
    /// Hasta cuándo está pagada la suscripción: el fin del periodo en curso o, si ya se pagó una renovación, el de esa.
    /// </summary>
    public Instant PeriodEnd { get; set; }

    /// <summary>
    /// Los días de calendario que faltan para <see cref="PeriodEnd"/> en la zona de la copropiedad: cero el día que vence,
    /// negativos después.
    /// </summary>
    public int DaysRemaining { get; set; }

    /// <summary>
    /// En qué punto de su vigencia está.
    /// </summary>
    public SubscriptionStatus Status { get; set; }

    /// <summary>
    /// Cuándo termina la gracia después del vencimiento.
    /// </summary>
    public Instant GraceEndsAt { get; set; }

    /// <summary>
    /// El saldo a favor de la copropiedad, en unidades mínimas de <see cref="Currency"/>.
    /// </summary>
    public long CreditBalance { get; set; }

    /// <summary>
    /// La moneda de los importes.
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// La periodicidad de la licencia en curso: lo que dura cada renovación.
    /// </summary>
    public BillingType BillingType { get; set; }

    /// <summary>
    /// Si hay una renovación o un cambio de licencia esperando su pago.
    /// </summary>
    public bool HasPaymentInProgress { get; set; }

    /// <summary>
    /// Los paquetes vigentes y los ya pagados que empiezan después (pendings/739); <c>isActive</c> dice si cuentan hoy.
    /// </summary>
    public List<Package.DataTransferObjects.PackageLineDto> Packages { get; set; } = [];

    /// <summary>
    /// Los atributos efectivos hoy: los de la licencia combinados con los de los paquetes que cuentan (pendings/739).
    /// </summary>
    public Dictionary<string, string> EffectiveAttributes { get; set; } = [];
}
