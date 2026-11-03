namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Una copropiedad a la que el Super Admin quiere ofrecerle una licencia privada (pendings/464).
/// </summary>
/// <param name="TenantId">La copropiedad.</param>
/// <param name="TenantName">Su nombre, como lo muestra el selector.</param>
public sealed record OfferedTenant(Guid TenantId, string TenantName);
