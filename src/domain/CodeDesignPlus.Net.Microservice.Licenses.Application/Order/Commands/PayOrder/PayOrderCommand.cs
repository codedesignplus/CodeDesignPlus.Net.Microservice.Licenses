using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;

[DtoGenerator]
public record PayOrderCommand(Guid Id, Domain.ValueObjects.Order Order, PaymentMethod PaymentMethod, Tenant Tenant) : IRequest;

public class Validator : AbstractValidator<PayOrderCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
        RuleFor(x => x.PaymentMethod).NotNull().WithMessage("The payment method information is required.");
        RuleFor(x => x.Order).NotNull().WithMessage("The Order information is required.");
        RuleFor(x => x.Tenant).NotNull().WithMessage("The Tenant information is required.");
    }
}
