using CreateTenantRequest = CodeDesignPlus.Net.gRpc.Clients.Services.Tenant.CreateTenantRequest;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Setup;
using TypeDocument = CodeDesignPlus.Net.ValueObjects.User.TypeDocument;
using Currency = CodeDesignPlus.Net.ValueObjects.Financial.Currency;
using Vo = CodeDesignPlus.Net.ValueObjects.Location;
using TenantSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.Tenant;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Setup;

/// <summary>
/// La compra de una copropiedad de un municipio sin localidades ni barrios llega a ms-tenants sin ellos.
/// </summary>
/// <remarks>
/// La conversión leía <c>Location.Locality.Id</c> sin comprobar, así que solo se podía comprar en Bogotá
/// (pendings/130).
/// </remarks>
public class TenantWithoutLocalityMappingTest
{
    static TenantWithoutLocalityMappingTest() => TypeAdapterConfig.GlobalSettings.Scan(typeof(MapsterConfigLicense).Assembly);

    private static TenantSnapshot TenantIn(Vo.Locality? locality, Vo.Neighborhood? neighborhood)
    {
        var currency = Currency.Create(Guid.NewGuid(), "Peso colombiano", "COP", "$", 2, 170);
        var location = Vo.Location.Create(
            Vo.Country.Create(Guid.NewGuid(), "Colombia", "CO", "COL", 170, "+57", "America/Bogota", currency),
            Vo.State.Create(Guid.NewGuid(), "Cundinamarca", "CUN"),
            Vo.City.Create(Guid.NewGuid(), "Chía", "America/Bogota"),
            locality,
            neighborhood,
            "Calle 10 # 5-20",
            "250001");

        return TenantSnapshot.Create(Guid.NewGuid(), "Conjunto Chía", null,
            TypeDocument.Create("NIT", "Número de Identificación Tributaria"), "901234567", "+573000000001", "admin@conjunto.com", location);
    }

    [Fact]
    public void Adapt_TenantWithoutLocalityAndNeighborhood_SendsThemAbsent()
    {
        var request = TenantIn(null, null).Adapt<CreateTenantRequest>();

        Assert.Equal("Chía", request.Location.City.Name);
        Assert.Null(request.Location.Locality);
        Assert.Null(request.Location.Neighborhood);
    }

    [Fact]
    public void Adapt_TenantWithLocalityAndNeighborhood_SendsBoth()
    {
        var locality = Vo.Locality.Create(Guid.NewGuid(), "Chapinero");
        var neighborhood = Vo.Neighborhood.Create(Guid.NewGuid(), "El Paraíso");

        var request = TenantIn(locality, neighborhood).Adapt<CreateTenantRequest>();

        Assert.Equal(locality.Id.ToString(), request.Location.Locality.Id);
        Assert.Equal("El Paraíso", request.Location.Neighborhood.Name);
    }
}
