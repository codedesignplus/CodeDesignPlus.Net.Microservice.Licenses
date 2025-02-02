namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetLicenseById;

public class GetLicenseByIdQueryHandler(ILicenseRepository repository, IMapper mapper, IUserContext user) : IRequestHandler<GetLicenseByIdQuery, LicenseDto>
{
    public Task<LicenseDto> Handle(GetLicenseByIdQuery request, CancellationToken cancellationToken)
    {
        return Task.FromResult<LicenseDto>(default!);
    }
}
