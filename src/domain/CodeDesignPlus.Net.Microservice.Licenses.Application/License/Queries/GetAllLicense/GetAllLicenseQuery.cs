namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetAllLicense;

public record GetAllLicenseQuery(Guid Id) : IRequest<LicenseDto>;

