using CodeDesignPlus.Net.Microservice.Licenses.Application.Setup;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Setup;

public class MapsterConfigTest
{
    [Fact]
    public void Configure_ShouldMapProperties_Success()
    {
        // Arrange
        var config = TypeAdapterConfig.GlobalSettings;
        config.Scan(typeof(MapsterConfigLicense).Assembly);

        // Act
        var mapper = new Mapper(config);

        // Assert
        Assert.NotNull(mapper);
    }
}
