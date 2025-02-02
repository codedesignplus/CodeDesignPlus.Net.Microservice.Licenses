namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetAllLicense;

public class GetAllLicenseQueryHandler(ILicenseRepository repository, IMapper mapper, IUserContext user) : IRequestHandler<GetAllLicenseQuery, LicenseDto>
{
    public Task<LicenseDto> Handle(GetAllLicenseQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult<LicenseDto>(default!);
    }
}
