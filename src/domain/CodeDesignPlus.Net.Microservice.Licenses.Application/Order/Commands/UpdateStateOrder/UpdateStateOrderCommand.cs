using CodeDesignPlus.Net.gRpc.Clients.Services.Payment;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;

[DtoGenerator]
public record UpdateStateOrderCommand(Guid ReferenceId, Domain.Enums.PaymentStatus PaymentStatus) : IRequest;

public class Validator : AbstractValidator<UpdateStateOrderCommand>
{
    public Validator()
    {
        RuleFor(x => x.ReferenceId).NotEmpty().NotNull();
    }
}
