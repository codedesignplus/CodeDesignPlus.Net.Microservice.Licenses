using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetTenantLicense;

/// <summary>
/// Query para obtener el snapshot inmutable de la licencia adquirida por un tenant.
/// Lee desde la orden de compra exitosa del tenant.
/// </summary>
/// <param name="TenantId">El GUID del tenant</param>
public record GetTenantLicenseQuery(Guid TenantId) : IRequest<LicenseTenant>;
