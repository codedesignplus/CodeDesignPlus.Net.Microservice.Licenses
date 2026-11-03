using CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Seeds;

namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Test.Seeds;

/// <summary>
/// La siembra de los paquetes de Colombia (pendings/739): los precios aprobados viven en el fichero, no en el código, y lo
/// que ya existe no se toca (regla 25).
/// </summary>
public class PackageSeedServiceTest
{
    /// <summary>
    /// Los tres paquetes del fichero son válidos y cuestan lo aprobado: Básico $1.990.000 y Plus $8.900.000 más IVA; la sala
    /// a la medida con su valor por defecto (una asamblea hasta 100 conectados con grabación) $465.000 más IVA.
    /// </summary>
    [Fact]
    public void Load_Colombia_SeedsTheApprovedPackagesAndPrices()
    {
        // Arrange
        var seeds = PackageSeedService.Load("co");

        // Act
        var totals = seeds.Select(x => PackagePricing.Quote(PackageAggregate.Create(x.Id, x.ToDefinition(), PackageSeedService.SystemUserId), null).SubTotal.Amount).ToList();

        // Assert
        Assert.Equal([199_000_000L, 890_000_000L, 46_500_000L], totals);
    }

    /// <summary>
    /// Un paquete que ya existe no se vuelve a crear ni se pisa: el Super Admin pudo cambiarlo.
    /// </summary>
    [Fact]
    public async Task SeedAsync_AnExistingPackage_IsNotTouched()
    {
        // Arrange
        var seeds = PackageSeedService.Load("co");
        var repository = new Mock<IPackageRepository>();
        repository.Setup(x => x.ExistsAsync<PackageAggregate>(seeds[0].Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        repository.Setup(x => x.CreateIfAbsentAsync(It.IsAny<PackageAggregate>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var service = new PackageSeedService(Mock.Of<IServiceScopeFactory>(), new ConfigurationBuilder().Build(), Mock.Of<ILogger<PackageSeedService>>());

        // Act
        var inserted = await service.SeedAsync(repository.Object, seeds, CancellationToken.None);

        // Assert
        Assert.Equal(2, inserted);
    }
}
