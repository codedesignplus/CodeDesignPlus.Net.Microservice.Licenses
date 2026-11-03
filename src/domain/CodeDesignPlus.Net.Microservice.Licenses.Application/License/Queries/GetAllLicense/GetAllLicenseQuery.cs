using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetAllLicense;

/// <summary>
/// Lista las licencias que cumplen el criterio.
/// </summary>
/// <param name="Criteria">El criterio de la pantalla.</param>
/// <param name="ForSaleOnly">
/// <c>true</c> en el catálogo público (landing y checkout): solo las que se ven en la landing, nunca las privadas
/// (pendings/464). <c>false</c> en la vista de plataforma, que ve todas.
/// </param>
public record GetAllLicenseQuery(C.Criteria Criteria, bool ForSaleOnly) : IRequest<Pagination<LicenseDto>>;

