namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetLicenseById;

public record GetLicenseByIdQuery(Guid Id) : IRequest<LicenseDto>;

public class Validator : AbstractValidator<GetLicenseByIdQuery>
{
    public Validator()
    {
        this.RuleFor(x => x.Id).NotEmpty();
    }
}