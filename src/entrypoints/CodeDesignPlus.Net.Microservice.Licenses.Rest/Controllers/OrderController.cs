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
    public async Task<IActionResult> PayOrder(Guid id, [FromBody] PayOrderDto data, CancellationToken cancellationToken)
    {
        data.Id = id;

        await mediator.Send(mapper.Map<PayOrderCommand>(data), cancellationToken);

        return NoContent();
    }
}


