namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetLicenseById;

public record GetLicenseByIdQuery(Guid Id) : IRequest<LicenseDto>;

