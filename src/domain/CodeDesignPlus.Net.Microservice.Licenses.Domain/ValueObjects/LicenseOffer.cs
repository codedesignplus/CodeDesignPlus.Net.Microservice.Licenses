namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Una copropiedad a la que se le ofrece una licencia privada, con quién se la ofreció y cuándo (pendings/464).
/// </summary>
/// <param name="TenantId">La copropiedad.</param>
/// <param name="TenantName">Su nombre cuando se le ofreció, para leerlo sin ir a ms-tenants.</param>
/// <param name="OfferedBy">Quién se la ofreció.</param>
/// <param name="OfferedAt">Cuándo se la ofreció.</param>
public sealed record LicenseOffer(Guid TenantId, string TenantName, Guid OfferedBy, Instant OfferedAt);
