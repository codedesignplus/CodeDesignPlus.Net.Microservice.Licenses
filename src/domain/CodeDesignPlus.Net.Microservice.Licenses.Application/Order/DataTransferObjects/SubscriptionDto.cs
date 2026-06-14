using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

/// <summary>
/// DTO que representa la suscripción activa de un tenant.
/// Contiene el snapshot completo de la licencia adquirida y el estado del pago/provisioning.
/// Money amounts in the License are converted to decimal for REST consumers.
/// </summary>
public class SubscriptionDto
{
    public required Guid Id { get; set; }
    public SubscriptionLicenseDto License { get; set; } = null!;
    public PaymentStatus PaymentStatus { get; set; }
    public ProvisioningStatus ProvisioningStatus { get; set; }
    public Instant CreatedAt { get; set; }
    public bool IsActive { get; set; }
}
