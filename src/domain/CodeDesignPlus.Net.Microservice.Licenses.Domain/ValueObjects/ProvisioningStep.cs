using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Un paso en la vida de una orden: su pago y, en una compra, el aprovisionamiento de la copropiedad y de su usuario.
/// </summary>
/// <remarks>
/// <see cref="StepName"/> es una clave estable y no un texto para leer: el detalle de la orden del Super Admin la traduce a su
/// nombre en español (pendings/674), así que cambiar una de estas constantes deja el historial guardado sin nombre.
/// </remarks>
/// <param name="StepName">El paso, como <see cref="Payment"/>.</param>
/// <param name="Status">Cómo terminó el paso.</param>
/// <param name="Timestamp">Cuándo se registró.</param>
/// <param name="Error">Por qué falló, si falló.</param>
public record ProvisioningStep(string StepName, ProvisioningStepStatus Status, Instant Timestamp, string? Error = null)
{
    /// <summary>
    /// El paso que registra el resultado del pago: su instante es el de la compra (pendings/264).
    /// </summary>
    public const string Payment = "Payment";

    /// <summary>
    /// El paso que lleva la licencia pagada a la copropiedad. Solo queda en el historial si falla: el pago está aprobado y
    /// la licencia no se pudo aplicar (pendings/738).
    /// </summary>
    public const string LicenseApplication = "LicenseApplication";

    /// <summary>
    /// El paso en que ms-tenants crea la copropiedad comprada.
    /// </summary>
    public const string TenantProvisioning = "TenantProvisioning";

    /// <summary>
    /// El paso en que ms-users da de alta a quien la compró en la copropiedad.
    /// </summary>
    public const string UserProvisioning = "UserProvisioning";

    /// <summary>
    /// El aprovisionamiento entero, cuando la conciliación se rinde tras agotar sus intentos (pendings/139).
    /// </summary>
    public const string Provisioning = "Provisioning";

    /// <summary>
    /// Los pasos que deben completarse para que la copropiedad comprada quede lista.
    /// </summary>
    public static readonly IReadOnlyList<string> RequiredForPurchase = [TenantProvisioning, UserProvisioning];
}
