using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Rest.Controllers;

/// <summary>
/// Controller for managing Orders.
/// </summary>
/// <param name="mediator">Mediator instance for sending commands and queries.</param>
/// <param name="mapper">Mapper instance for mapping between DTOs and commands/queries.</param>
[Route("api/[controller]")]
[ApiController]
public class OrderController(IMediator mediator, IMapper mapper) : ControllerBase
{

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetOrderByIdQuery(id), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Pay for a License by its ID. 
    /// </summary>
    /// <param name="id">The unique identifier of the License.</param>
    /// <param name="data">Data for paying the License.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP status code 204 (No Content).</returns>
    [HttpPost("{id}")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PaymentResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> PayOrder(Guid id, [FromBody] PayOrderDto data, CancellationToken cancellationToken)
    {
        data.Id = id;

        var response = await mediator.Send(mapper.Map<PayOrderCommand>(data), cancellationToken);

        return Ok(response);
    }

    /// <summary>
    /// Update the state of an Order by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the Order.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <response code="200">Returns the updated PaymentResponse.</response>
    /// <response code="400">If the request is invalid.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="403">If the user does not have permission to update the Order.</response>
    /// <response code="500">If an internal server error occurs.</response>
    [HttpPut("{id}/state")]
    [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(PaymentResponse))]
    [ProducesResponseType(StatusCodes.Status400BadRequest, Type = typeof(ProblemDetails))]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError, Type = typeof(ProblemDetails))]
    public async Task<IActionResult> UpdateStateOrder(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new UpdateStateOrderCommand(id), cancellationToken);

        return Ok(result);
    }
}


