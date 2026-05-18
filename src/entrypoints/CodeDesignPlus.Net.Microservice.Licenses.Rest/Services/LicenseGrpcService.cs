using CodeDesignPlus.Net.Microservice.Licenses.Rest.Grpc;
using CodeDesignPlus.Net.Microservice.Licenses.Domain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;

namespace CodeDesignPlus.Net.Microservice.Licenses.Rest.Services;

/// <summary>
/// Servicio gRPC que expone información de licencias para el ecosistema de microservicios.
/// Utilizado principalmente por el LicenseMiddleware del SDK de seguridad para poblar
/// el caché del tenant con los módulos de la licencia comprada.
/// </summary>
public class LicenseGrpcService(
    IOrderRepository orderRepository,
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

        var order = await orderRepository.FindSucceededOrderByTenantAsync(tenantId, context.CancellationToken);

        if (order is null)
        {
            logger.LogWarning("GetTenantLicense: No successful order found for tenant {TenantId}", tenantId);
            throw new RpcException(new Status(StatusCode.NotFound, $"No active license found for tenant {tenantId}."));
        }

        var license = order.GetLicenseTenant();

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
}
