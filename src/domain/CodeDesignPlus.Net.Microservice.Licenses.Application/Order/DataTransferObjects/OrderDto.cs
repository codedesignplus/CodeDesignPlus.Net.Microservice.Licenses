using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

public class OrderDto : IDtoBase
{
    public required Guid Id { get; set; }
    public Guid IdLicense { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; } = null!;
    public Buyer Buyer { get; private set; } = null!;
    public Tenant TenantDetail { get; private set; } = null!;
    public PaymentResponse Response { get; private set; } = null!;
}