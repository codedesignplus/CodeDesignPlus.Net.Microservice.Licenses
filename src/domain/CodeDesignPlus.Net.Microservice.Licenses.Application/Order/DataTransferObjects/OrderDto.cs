using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

public class OrderDto : IDtoBase
{
    public required Guid Id { get; set; }
    public Guid IdLicense { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = null!;
    public Buyer Buyer { get; set; } = null!;
    public Tenant TenantDetail { get; set; } = null!;
    public PaymentResponse Response { get; set; } = null!;
}