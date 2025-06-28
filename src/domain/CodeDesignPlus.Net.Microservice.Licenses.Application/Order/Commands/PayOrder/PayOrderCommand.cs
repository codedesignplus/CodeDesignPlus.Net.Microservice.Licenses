using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;

[DtoGenerator]
public record PayOrderCommand(Guid Id, Buyer Buyer, Domain.ValueObjects.License License, PaymentMethod PaymentMethod, Tenant TenantDetail) : IRequest;

public class Validator : AbstractValidator<PayOrderCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
        RuleFor(x => x.PaymentMethod).NotNull().WithMessage("The payment method information is required.");
        RuleFor(x => x.Buyer).NotNull().WithMessage("The Buyer information is required.");
        RuleFor(x => x.TenantDetail).NotNull().WithMessage("The Tenant information is required.");
        RuleFor(x => x.License).NotNull().WithMessage("The License information is required.");
    }
}
