using CodeDesignPlus.Net.gRpc.Clients.Services.Payment;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;

/// <summary>
/// Aplica a una orden el resultado de su pago.
/// </summary>
/// <param name="ReferenceId">La orden.</param>
/// <param name="PaymentStatus">El resultado del pago.</param>
/// <param name="FailureReason">Por qué no se aprobó, si no se aprobó (pendings/738).</param>
/// <param name="ProviderMessage">Lo que dijo el banco o la pasarela, si no se aprobó.</param>
[DtoGenerator]
public record UpdateStateOrderCommand(Guid ReferenceId, Domain.Enums.PaymentStatus PaymentStatus, Domain.Enums.PaymentFailureReason? FailureReason = null, string? ProviderMessage = null) : IRequest;

public class Validator : AbstractValidator<UpdateStateOrderCommand>
{
    public Validator()
    {
        RuleFor(x => x.ReferenceId).NotEmpty().NotNull();
    }
}
