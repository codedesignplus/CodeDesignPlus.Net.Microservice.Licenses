using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Rest.Test;

/// <summary>
/// El tipo de documento viaja con el codigo DIAN del catalogo del pais (pendings/663): cada codigo se factura tal cual y
/// las abreviaturas viejas (CC, NIT) ya no estan.
/// </summary>
public class DocumentTypeCodesConfigurationTest
{
    [Theory]
    [InlineData("CodeDesignPlus.Net.Microservice.Licenses.Rest")]
    [InlineData("CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker")]
    public void Appsettings_IdentificationTypeCodes_MapEachDianCodeToItself(string entrypoint)
    {
        // Arrange
        var directory = new DirectoryInfo(System.AppContext.BaseDirectory);

        while (directory is not null && !System.IO.File.Exists(Path.Combine(directory.FullName, "CodeDesignPlus.Net.Microservice.Licenses.sln")))
        {
            directory = directory.Parent;
        }

        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(directory!.FullName, "src", "entrypoints", entrypoint, "appsettings.json")).Build();

        // Act
        var codes = configuration.GetSection("PlatformInvoicing:IdentificationTypeCodes").GetChildren().ToDictionary(x => x.Key, x => x.Value);

        // Assert
        Assert.Equal(["11", "12", "13", "21", "22", "31", "41", "42", "47", "48", "50", "91"], codes.Keys.Order().ToArray());
        Assert.All(codes, x => Assert.Equal(x.Key, x.Value));
    }
}
