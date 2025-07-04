using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

public class OrderDto : IDtoBase
{
    public required Guid Id { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = null!;
    public Buyer Buyer { get; set; } = null!;
    public Domain.ValueObjects.License License { get; set; } = null!;
    public Tenant TenantDetail { get; set; } = null!;
    public PaymentResponse? PaymentResponse { get; set; } = null!;
    public Instant CreatedAt { get; set; }
    public bool IsActive { get; set; }
}