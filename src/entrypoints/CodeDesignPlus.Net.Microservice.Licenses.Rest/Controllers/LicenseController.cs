using Microsoft.AspNetCore.Authorization;

namespace CodeDesignPlus.Net.Microservice.Licenses.Rest.Controllers;

/// <summary>
/// Controller for managing the Licenses.
/// </summary>
/// <param name="mediator">Mediator instance for sending commands and queries.</param>
/// <param name="mapper">Mapper instance for mapping between DTOs and commands/queries.</param>
[Route("api/[controller]")]
[ApiController]
public class LicenseController(IMediator mediator, IMapper mapper) : ControllerBase
{
    /// <summary>
    /// Get all Licenses.
    /// </summary>
    /// <param name="criteria">Criteria for filtering and sorting the Licenses.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of Licenses.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetLicenses([FromQuery] C.Criteria criteria, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetAllLicenseQuery(criteria), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Get a License by its ID.
    /// </summary>
    /// <param name="id">The unique identifier of the License.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The License.</returns>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetLicenseById(Guid id, CancellationToken cancellationToken)
    {
        var result = await mediator.Send(new GetLicenseByIdQuery(id), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Create a new License.
    /// </summary>
    /// <param name="data">Data for creating the License.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP status code 204 (No Content).</returns>
    [HttpPost]
    public async Task<IActionResult> CreateLicense([FromBody] CreateLicenseDto data, CancellationToken cancellationToken)
    {
        await mediator.Send(mapper.Map<CreateLicenseCommand>(data), cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Add modules to an existing License.
    /// </summary>
    /// <param name="id">The unique identifier of the License.</param>
    /// <param name="data">Data for adding modules to the License.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP status code 204 (No Content).</returns>
    [HttpPost("{id}/module")]
    public async Task<IActionResult> AddModules(Guid id, [FromBody] AddModuleDto data, CancellationToken cancellationToken)
    {
        data.Id = id;

        await mediator.Send(mapper.Map<AddModuleCommand>(data), cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Update an existing License.
    /// </summary>
    /// <param name="id">The unique identifier of the License.</param>
    /// <param name="data">Data for updating the License.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP status code 204 (No Content).</returns>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateLicense(Guid id, [FromBody] UpdateLicenseDto data, CancellationToken cancellationToken)
    {
        data.Id = id;

        await mediator.Send(mapper.Map<UpdateLicenseCommand>(data), cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Delete an existing License.
    /// </summary>
    /// <param name="id">The unique identifier of the License.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP status code 204 (No Content).</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteLicense(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeleteLicenseCommand(id), cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Remove a module from an existing License.
    /// </summary>
    /// <param name="id">The unique identifier of the License.</param>
    /// <param name="idModule">The unique identifier of the module to be removed.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>HTTP status code 204 (No Content).</returns>
    [HttpDelete("{id}/module/{idModule}")]
    public async Task<IActionResult> RemoveModule(Guid id, Guid idModule, CancellationToken cancellationToken)
    {
        await mediator.Send(new RemoveModuleCommand(id, idModule), cancellationToken);

        return NoContent();
    }
}