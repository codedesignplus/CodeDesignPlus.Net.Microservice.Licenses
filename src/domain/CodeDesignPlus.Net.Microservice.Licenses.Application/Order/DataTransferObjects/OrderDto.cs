using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Payment;
using CodeDesignPlus.Net.ValueObjects.User;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

public class OrderDto : IDtoBase
{
    public required Guid Id { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = null!;
    public Buyer Buyer { get; set; } = null!;
    public Domain.ValueObjects.License License { get; set; } = null!;
    public Tenant TenantDetail { get; set; } = null!;
    public PaymentStatus PaymentStatus { get; set; }
    public Instant CreatedAt { get; set; }
    public bool IsActive { get; set; }
}