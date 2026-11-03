using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.ChangeSubscription;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.RenewSubscription;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Queries.GetChangeOptions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.PurchasePackage;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Rest.Controllers;

/// <summary>
/// La suscripción de la copropiedad autenticada: renovarla y cambiarla por una licencia mayor (pendings/735, 738).
/// </summary>
/// <remarks>
/// Todo actúa sobre la copropiedad del token. Quién puede usarlo lo decide el RBAC (en Kappali, la administración desde
/// Configuración › Organización); el micro no comprueba cargos.
/// </remarks>
/// <param name="mediator">Mediator instance for sending commands and queries.</param>
/// <param name="mapper">Mapper instance for mapping between DTOs and commands.</param>
/// <param name="userContext">User context for accessing the authenticated user's tenant.</param>
[Route("api/[controller]")]
[ApiController]
public class SubscriptionController(IMediator mediator, IMapper mapper, IUserContext userContext) : ControllerBase
{
    /// <summary>
    /// Renew the license of the authenticated tenant for one more period.
    /// </summary>
    /// <param name="data">The order id, generated once per attempt by the client.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The payment was initiated (redirect to the provider) or, when the credit balance covers it, confirmed without payment.</response>
    /// <response code="400">If the request is invalid or the license can no longer be renewed.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission.</response>
    /// <response code="500">If an internal server error occurs.</response>
    [HttpPost("renew")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PaymentResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> Renew([FromBody] RenewSubscriptionDto data, CancellationToken cancellationToken)
    {
        var response = await mediator.Send(mapper.Map<RenewSubscriptionCommand>(data), cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Get the licenses the authenticated tenant can change to, each one with its quote.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The licenses, cheapest first; empty when there is no greater license.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission.</response>
    /// <response code="500">If an internal server error occurs.</response>
    [HttpGet("change-options")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(List<ChangeOptionDto>))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> GetChangeOptions(CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetChangeOptionsQuery(userContext.Tenant), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Change the license of the authenticated tenant to a greater one.
    /// </summary>
    /// <param name="data">The order id, generated once per attempt by the client, and the license to change to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The payment was initiated (redirect to the provider) or, when the credit covers it, confirmed without payment.</response>
    /// <response code="400">If the request is invalid or the license is not a greater one offered to the tenant.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission.</response>
    /// <response code="500">If an internal server error occurs.</response>
    [HttpPost("change")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PaymentResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> Change([FromBody] ChangeSubscriptionDto data, CancellationToken cancellationToken)
    {
        var response = await mediator.Send(mapper.Map<ChangeSubscriptionCommand>(data), cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Quote the renewal of the license with its add-on packages (pendings/739).
    /// </summary>
    /// <param name="data">The packages to renew; absent renews the current ones as they are.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The quote of the renewal.</response>
    [HttpPost("renewal-quote")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(RenewalQuoteDto))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> RenewalQuote([FromBody] RenewalQuoteRequest data, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetRenewalQuoteQuery(userContext.Tenant, data?.Packages), cancellationToken));
    }

    /// <summary>
    /// Buy or extend an add-on package for the authenticated tenant (pendings/739).
    /// </summary>
    /// <param name="data">The order id, generated once per attempt by the client, the package and the chosen values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">The payment was initiated or, when the credit covers it, confirmed without payment.</response>
    [HttpPost("packages")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PaymentResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> PurchasePackage([FromBody] PurchasePackageDto data, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(mapper.Map<PurchasePackageCommand>(data), cancellationToken));
    }
}

/// <summary>
/// The body of the renewal quote.
/// </summary>
/// <param name="Packages">The packages to renew; absent renews the current ones as they are.</param>
public sealed record RenewalQuoteRequest(List<PackageSelectionInput>? Packages);
