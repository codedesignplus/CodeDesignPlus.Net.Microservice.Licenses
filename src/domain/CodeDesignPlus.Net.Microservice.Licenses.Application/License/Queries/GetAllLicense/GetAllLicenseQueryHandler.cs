namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetAllLicense;

public class GetAllLicenseQueryHandler(ILicenseRepository repository, IMapper mapper) : IRequestHandler<GetAllLicenseQuery, List<LicenseDto>>
{
    public async Task<List<LicenseDto>> Handle(GetAllLicenseQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var licenses = await repository.MatchingAsync<LicenseAggregate>(request.Criteria, cancellationToken);

        return mapper.Map<List<LicenseDto>>(licenses);
    }
}
