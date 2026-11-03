using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetLicenseById;

/// <summary>
/// Lee una licencia.
/// </summary>
/// <param name="Id">La licencia.</param>
/// <param name="ForSaleOnly">
/// <c>true</c> en el catálogo público: una licencia privada responde como no encontrada (pendings/464).
/// </param>
public record GetLicenseByIdQuery(Guid Id, bool ForSaleOnly) : IRequest<LicenseDto>;

public class Validator : AbstractValidator<GetLicenseByIdQuery>
{
    public Validator()
    {
        this.RuleFor(x => x.Id).NotEmpty();
    }
}