namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetAllLicense;

public record GetAllLicenseQuery(C.Criteria Criteria) : IRequest<List<LicenseDto>>;

