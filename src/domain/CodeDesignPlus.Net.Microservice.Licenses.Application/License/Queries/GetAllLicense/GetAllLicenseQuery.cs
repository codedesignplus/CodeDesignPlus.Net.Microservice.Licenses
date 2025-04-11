using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetAllLicense;

public record GetAllLicenseQuery(C.Criteria Criteria) : IRequest<Pagination<LicenseDto>>;

