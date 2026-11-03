using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.RenewSubscription;

/// <summary>
/// Renueva la licencia de la copropiedad autenticada por un periodo más (pendings/735).
/// </summary>
/// <param name="Id">El identificador de la orden, generado por el cliente una vez por intento (regla 66).</param>
/// <param name="AcceptedTermsVersion">La versión de los términos que aceptó, si la pantalla los pidió (pendings/662).</param>
[DtoGenerator]
public record RenewSubscriptionCommand(Guid Id, string? AcceptedTermsVersion) : IRequest<PaymentResponse>;

/// <summary>
/// Valida el comando.
/// </summary>
public class Validator : AbstractValidator<RenewSubscriptionCommand>
{
    /// <summary>
    /// Crea el validador.
    /// </summary>
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.AcceptedTermsVersion).MaximumLength(FieldLength.Name);
    }
}
