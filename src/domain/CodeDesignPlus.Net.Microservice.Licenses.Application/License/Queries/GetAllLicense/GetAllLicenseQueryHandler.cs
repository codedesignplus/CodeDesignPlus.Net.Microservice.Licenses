using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetAllLicense;

public class GetAllLicenseQueryHandler(ILicenseRepository repository, IMapper mapper) : IRequestHandler<GetAllLicenseQuery, Pagination<LicenseDto>>
{
    public async Task<Pagination<LicenseDto>> Handle(GetAllLicenseQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var licenses = await repository.MatchingAsync<LicenseAggregate>(request.Criteria, cancellationToken);

        return mapper.Map<Pagination<LicenseDto>>(licenses);
    }
}
