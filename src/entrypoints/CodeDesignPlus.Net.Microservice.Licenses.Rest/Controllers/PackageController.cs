using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Commands.CreatePackage;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Commands.DeletePackage;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Commands.UpdatePackage;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Queries.GetAllPackages;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Queries.GetAvailablePackages;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Queries.GetPackageById;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Queries.QuotePackage;
using Microsoft.AspNetCore.Authorization;

namespace CodeDesignPlus.Net.Microservice.Licenses.Rest.Controllers;

/// <summary>
/// The catalog of add-on packages of the license (pendings/739): public for the landing, the quote, and the platform view
/// of the Super Admin. Who can use each route is decided by the RBAC; the service does not check roles.
/// </summary>
/// <param name="mediator">Mediator instance for sending commands and queries.</param>
/// <param name="mapper">Mapper instance for mapping between DTOs and commands.</param>
/// <param name="userContext">User context for the tenant of the session.</param>
[Route("api/[controller]")]
[ApiController]
public class PackageController(IMediator mediator, IMapper mapper, IUserContext userContext) : ControllerBase
{
    /// <summary>
    /// Get the public packages: the ones shown on the landing page.
    /// </summary>
    /// <param name="criteria">Criteria for filtering and sorting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The packages.</returns>
    [HttpGet]
    [AllowAnonymous]
    public async Task<IActionResult> GetPackages([FromQuery] C.Criteria criteria, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetAllPackagesQuery(criteria, ForSaleOnly: true), cancellationToken));
    }

    /// <summary>
    /// Get every package, public and private: the platform view of the Super Admin.
    /// </summary>
    /// <param name="criteria">Criteria for filtering and sorting.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The packages.</returns>
    [HttpGet("all")]
    public async Task<IActionResult> GetAllPackages([FromQuery] C.Criteria criteria, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetAllPackagesQuery(criteria, ForSaleOnly: false), cancellationToken));
    }

    /// <summary>
    /// Get the packages the tenant of the session can buy or extend: the public ones and the private ones offered to it.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The packages, cheapest first.</returns>
    [HttpGet("available")]
    public async Task<IActionResult> GetAvailablePackages(CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetAvailablePackagesQuery(userContext.Tenant), cancellationToken));
    }

    /// <summary>
    /// Get a public package by its id; a private one answers as not found.
    /// </summary>
    /// <param name="id">The package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The package.</returns>
    [HttpGet("{id}")]
    [AllowAnonymous]
    public async Task<IActionResult> GetPackageById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetPackageByIdQuery(id, ForSaleOnly: true), cancellationToken));
    }

    /// <summary>
    /// Get any package by its id: the platform view of the Super Admin.
    /// </summary>
    /// <param name="id">The package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The package.</returns>
    [HttpGet("all/{id}")]
    public async Task<IActionResult> GetAnyPackageById(Guid id, CancellationToken cancellationToken)
    {
        return Ok(await mediator.Send(new GetPackageByIdQuery(id, ForSaleOnly: false), cancellationToken));
    }

    /// <summary>
    /// Quote a package with the values the customer chose. Without a session, the catalog price; with a session, also the
    /// credit of an extension, the credit balance and the period of the tenant.
    /// </summary>
    /// <param name="data">The package and the chosen values.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The quote.</returns>
    [HttpPost("quote")]
    [AllowAnonymous]
    public async Task<IActionResult> Quote([FromBody] PackageSelectionInput data, CancellationToken cancellationToken)
    {
        // Solo la copropiedad de una sesión: sin ella, la cabecera X-Tenant no basta para ver el saldo de otra.
        var tenant = userContext.IsAuthenticated ? userContext.Tenant : Guid.Empty;

        return Ok(await mediator.Send(new QuotePackageQuery(data.PackageId, data.Selections, tenant), cancellationToken));
    }

    /// <summary>
    /// Create a package.
    /// </summary>
    /// <param name="data">The package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpPost]
    public async Task<IActionResult> CreatePackage([FromBody] CreatePackageDto data, CancellationToken cancellationToken)
    {
        await mediator.Send(mapper.Map<CreatePackageCommand>(data), cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Update a package.
    /// </summary>
    /// <param name="id">The package.</param>
    /// <param name="data">The package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePackage(Guid id, [FromBody] UpdatePackageDto data, CancellationToken cancellationToken)
    {
        data.Id = id;

        await mediator.Send(mapper.Map<UpdatePackageCommand>(data), cancellationToken);

        return NoContent();
    }

    /// <summary>
    /// Delete a package: it is no longer sold nor renewed; what was bought lasts until its end.
    /// </summary>
    /// <param name="id">The package.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>No content.</returns>
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePackage(Guid id, CancellationToken cancellationToken)
    {
        await mediator.Send(new DeletePackageCommand(id), cancellationToken);

        return NoContent();
    }
}
