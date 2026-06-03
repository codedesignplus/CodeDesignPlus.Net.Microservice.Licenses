using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetTenantLicense;

/// <summary>
/// Handler para obtener el snapshot inmutable de la licencia de un tenant.
/// Busca la orden exitosa del tenant y retorna el LicenseTenant con los módulos comprados.
/// </summary>
public class GetTenantLicenseQueryHandler(IOrderRepository repository) : IRequestHandler<GetTenantLicenseQuery, LicenseTenant>
{
    public async Task<LicenseTenant> Handle(GetTenantLicenseQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);
        ApplicationGuard.GuidIsEmpty(request.TenantId, Errors.TenantIdIsRequired);

        var order = await repository.FindSucceededOrderByTenantAsync(request.TenantId, cancellationToken);

        ApplicationGuard.IsNull(order, Errors.OrderNotFound);

        return order.GetLicenseTenant();
    }
}
