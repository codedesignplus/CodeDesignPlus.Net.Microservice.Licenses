using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

public class OrderDto : IDtoBase
{
    public required Guid Id { get; set; }
    public Guid IdLicense { get; private set; }
    public PaymentMethod PaymentMethod { get; private set; } = null!;
    public Buyer Buyer { get; private set; } = null!;
    public Tenant Organization { get; private set; } = null!;
    public string? Error { get; private set; }
    public bool IsSuccess { get; private set; }    
    public PaymentResponse Response { get; private set; } = null!;
}