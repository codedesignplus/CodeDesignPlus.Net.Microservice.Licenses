using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.User;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;

[DtoGenerator]
/// <summary>
/// La compra inicial de una licencia, que crea la copropiedad.
/// </summary>
/// <param name="Id">La orden, generada por el cliente (regla 66).</param>
/// <param name="Buyer">Quien compra.</param>
/// <param name="License">La licencia y su periodicidad.</param>
/// <param name="TenantDetail">La copropiedad que se crea.</param>
/// <param name="AcceptedTermsVersion">La versión de los términos que aceptó la casilla del checkout (pendings/662).</param>
/// <param name="Packages">Los paquetes complementarios que se compran con la licencia, en el mismo pago (pendings/739).</param>
public record PayOrderCommand(Guid Id, BuyerDto Buyer, DataTransferObjects.LicenseDto License, Tenant TenantDetail, string AcceptedTermsVersion, List<PackageSelectionInput>? Packages = null) : IRequest<PaymentResponse>;

public class Validator : AbstractValidator<PayOrderCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
        RuleFor(x => x.Buyer).NotNull();
        RuleFor(x => x.TenantDetail).NotNull();
        RuleFor(x => x.License).NotNull();
        RuleFor(x => x.AcceptedTermsVersion).NotEmpty().MaximumLength(FieldLength.Name);
    }
}
