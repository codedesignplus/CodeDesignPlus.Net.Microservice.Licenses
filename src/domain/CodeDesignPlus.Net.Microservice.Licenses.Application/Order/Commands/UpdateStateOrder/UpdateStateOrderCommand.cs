using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;

[DtoGenerator]
public record UpdateStateOrderCommand(Guid Id) : IRequest<PaymentResponse>;

public class Validator : AbstractValidator<UpdateStateOrderCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
    }
}
