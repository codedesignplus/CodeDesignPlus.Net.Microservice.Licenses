using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Payment;
using CodeDesignPlus.Net.ValueObjects.User;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

public class OrderDto : IDtoBase
{
    public required Guid Id { get; set; }
    public Guid PaymentId { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = null!;
    public Buyer Buyer { get; set; } = null!;
    public Domain.ValueObjects.License License { get; set; } = null!;
    public Tenant TenantDetail { get; set; } = null!;
    public PaymentStatus PaymentStatus { get; set; }
    public ProvisioningStatus ProvisioningStatus { get; set; }
    public List<Domain.ValueObjects.ProvisioningStep> ProvisioningHistory { get; set; } = [];

    /// <summary>
    /// La referencia en ms-filestorage del recibo de compra, si ya se genero.
    /// </summary>
    /// <remarks>
    /// Es la referencia, no la URL: quien lo muestre pide una firma nueva, porque la de ms-filestorage
    /// caduca a los 7 dias.
    /// </remarks>
    public Domain.ValueObjects.FileAttachment? Receipt { get; set; }
    public Instant CreatedAt { get; set; }
    public bool IsActive { get; set; }
}