using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.ChangeSubscription;

/// <summary>
/// Cambia la licencia de la copropiedad autenticada por una mayor (pendings/738).
/// </summary>
/// <param name="Id">El identificador de la orden, generado por el cliente una vez por intento (regla 66).</param>
/// <param name="LicenseId">La licencia del catálogo a la que se cambia.</param>
[DtoGenerator]
public record ChangeSubscriptionCommand(Guid Id, Guid LicenseId) : IRequest<PaymentResponse>;

/// <summary>
/// Valida el comando.
/// </summary>
public class Validator : AbstractValidator<ChangeSubscriptionCommand>
{
    /// <summary>
    /// Crea el validador.
    /// </summary>
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.LicenseId).NotEmpty();
    }
}
