namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetLicenseById;

public class GetLicenseByIdQueryHandler(ILicenseRepository repository, IMapper mapper, ICacheManager cacheManager) : IRequestHandler<GetLicenseByIdQuery, LicenseDto>
{
    public async Task<LicenseDto> Handle(GetLicenseByIdQuery request, CancellationToken cancellationToken)
    {        
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var exists = await cacheManager.ExistsAsync(request.Id.ToString());

        if (exists)
            return await cacheManager.GetAsync<LicenseDto>(request.Id.ToString());

        var license = await repository.FindAsync<LicenseAggregate>(request.Id, cancellationToken);

        ApplicationGuard.IsNull(license, Errors.LicenseNotFound);

        var dto = mapper.Map<LicenseDto>(license);

        await cacheManager.SetAsync(request.Id.ToString(), license);

        return dto;
    }
}
