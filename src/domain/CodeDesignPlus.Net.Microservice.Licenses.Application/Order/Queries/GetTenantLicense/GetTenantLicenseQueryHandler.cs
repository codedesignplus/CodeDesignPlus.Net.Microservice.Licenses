using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetTenantLicense;

/// <summary>
/// Handler para obtener el snapshot inmutable de la licencia de un tenant.
/// </summary>
/// <remarks>
/// Sale de su suscripción: los módulos y atributos de la orden en curso, vigente hasta el fin de lo pagado, contando las
/// renovaciones pagadas por adelantado (pendings/735). Antes salía de la última orden, con un fin calculado desde su
/// creación.
/// </remarks>
public class GetTenantLicenseQueryHandler(IOrderRepository repository) : IRequestHandler<GetTenantLicenseQuery, LicenseTenant>
{
    public async Task<LicenseTenant> Handle(GetTenantLicenseQuery request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);
        ApplicationGuard.GuidIsEmpty(request.TenantId, Errors.TenantIdIsRequired);

        var subscription = SubscriptionTimeline.From(await repository.FindPaidByTenantAsync(request.TenantId, cancellationToken));

        ApplicationGuard.IsNull(subscription, Errors.OrderNotFound);

        return subscription.ToLicenseTenant(SystemClock.Instance.GetCurrentInstant());
    }
}
