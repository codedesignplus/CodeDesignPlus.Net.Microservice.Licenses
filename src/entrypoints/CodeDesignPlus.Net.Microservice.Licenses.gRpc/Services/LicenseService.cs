using CodeDesignPlus.Net.Microservice.Licenses.Application;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetTenantLicense;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using MediatR;

namespace CodeDesignPlus.Net.Microservice.Licenses.gRpc.Services;

/// <summary>
/// Servicio gRPC que expone información de licencias para el ecosistema de microservicios.
/// Utilizado principalmente por el LicenseMiddleware del SDK de seguridad para poblar
/// el caché del tenant con los módulos de la licencia comprada.
/// </summary>
public class LicenseGrpcService(
    IMediator mediator,
    ILogger<LicenseGrpcService> logger) : LicenseService.LicenseServiceBase
{
    /// <summary>
    /// Retorna el snapshot inmutable de la licencia adquirida por el tenant.
    /// Lee desde la orden de compra, garantizando que los módulos reflejan
    /// exactamente lo que el cliente compró, sin importar cambios posteriores al catálogo.
    /// </summary>
    public override async Task<GetTenantLicenseResponse> GetTenantLicense(
        GetTenantLicenseRequest request,
        ServerCallContext context)
    {
        if (!Guid.TryParse(request.TenantId, out var tenantId))
        {
            logger.LogWarning("GetTenantLicense: Invalid tenant ID format: {TenantId}", request.TenantId);
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid tenant ID format."));
        }

        try
        {
            var query = new GetTenantLicenseQuery(tenantId);
            var license = await mediator.Send(query, context.CancellationToken);

            var response = new GetTenantLicenseResponse
            {
                LicenseId = license.Id.ToString(),
                Name = license.Name,
                StartDate = Timestamp.FromDateTimeOffset(license.StartDate.ToDateTimeOffset()),
                EndDate = Timestamp.FromDateTimeOffset(license.EndDate.ToDateTimeOffset()),
            };

            response.Modules.AddRange(license.Modules.Select(m => new LicenseModuleResponse
            {
                Id = m.Id.ToString(),
                Name = m.Name,
                Description = m.Description
            }));

            foreach (var kv in license.Metadata)
                response.Metadata[kv.Key] = kv.Value;

            logger.LogInformation(
                "GetTenantLicense: Returned license {LicenseName} with {ModuleCount} modules for tenant {TenantId}",
                license.Name, license.Modules.Count, tenantId);

            return response;
        }
        catch (ApplicationException ex) when (ex.Message.Contains(Errors.OrderNotFound))
        {
            logger.LogWarning("GetTenantLicense: No successful order found for tenant {TenantId}", tenantId);
            throw new RpcException(new Status(StatusCode.NotFound, $"No active license found for tenant {tenantId}."));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "GetTenantLicense: Unexpected error for tenant {TenantId}", tenantId);
            throw new RpcException(new Status(StatusCode.Internal, "An unexpected error occurred."));
        }
    }
}
