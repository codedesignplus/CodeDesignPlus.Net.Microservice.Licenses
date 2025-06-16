using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;

[DtoGenerator]
public record PayOrderCommand(Guid Id, OrderDetail OrderDetail, PaymentMethod PaymentMethod, Tenant TenantDetail) : IRequest;

public class Validator : AbstractValidator<PayOrderCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
        RuleFor(x => x.PaymentMethod).NotNull().WithMessage("The payment method information is required.");
        RuleFor(x => x.OrderDetail).NotNull().WithMessage("The Order information is required.");
        RuleFor(x => x.TenantDetail).NotNull().WithMessage("The Tenant information is required.");
    }
}
