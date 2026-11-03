using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.PurchasePackage;

/// <summary>
/// Compra o amplía un paquete para la copropiedad autenticada (pendings/739).
/// </summary>
/// <param name="Id">El identificador de la orden, generado por el cliente una vez por intento (regla 66).</param>
/// <param name="PackageId">El paquete del catálogo.</param>
/// <param name="Selections">Lo elegido por clave de componente; vacío en un fijo.</param>
/// <param name="AcceptedTermsVersion">La versión de los términos que aceptó.</param>
[DtoGenerator]
public record PurchasePackageCommand(Guid Id, Guid PackageId, Dictionary<string, string>? Selections, string AcceptedTermsVersion) : IRequest<PaymentResponse>;

/// <summary>
/// Valida el comando.
/// </summary>
public class Validator : AbstractValidator<PurchasePackageCommand>
{
    /// <summary>
    /// Crea el validador.
    /// </summary>
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.PackageId).NotEmpty();
        RuleFor(x => x.AcceptedTermsVersion).NotEmpty().MaximumLength(FieldLength.Name);
    }
}
