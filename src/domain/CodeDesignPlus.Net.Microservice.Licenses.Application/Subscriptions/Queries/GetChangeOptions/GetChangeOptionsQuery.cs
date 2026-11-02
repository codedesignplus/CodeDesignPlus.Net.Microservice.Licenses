using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Queries.GetChangeOptions;

/// <summary>
/// Las licencias a las que se puede cambiar la copropiedad autenticada, cada una con su cotización (pendings/738).
/// </summary>
/// <param name="TenantId">La copropiedad (del token).</param>
public record GetChangeOptionsQuery(Guid TenantId) : IRequest<List<ChangeOptionDto>>;
