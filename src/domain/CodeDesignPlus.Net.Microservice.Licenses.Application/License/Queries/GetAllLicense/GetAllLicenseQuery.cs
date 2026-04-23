using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetAllLicense;

public record GetAllLicenseQuery(C.Criteria Criteria) : IRequest<Pagination<LicenseDto>>;

