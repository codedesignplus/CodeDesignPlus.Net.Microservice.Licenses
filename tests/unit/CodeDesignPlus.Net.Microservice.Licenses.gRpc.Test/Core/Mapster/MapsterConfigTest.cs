namespace CodeDesignPlus.Net.Microservice.Licenses.gRpc.Test.Core.Mapster;

/// <summary>
/// Cubre el arranque del mapeo del entrypoint gRPC.
/// </summary>
/// <remarks>
/// El <c>Configure()</c> de este entrypoint esta vacio **a proposito**: <c>LicenseService</c> arma sus
/// respuestas protobuf a mano y no hay un solo <c>Adapt</c> ni <c>IMapper</c> en todo el proyecto.
/// <para>
/// La version anterior de este test afirmaba <c>Assert.NotEmpty(config.RuleMap)</c>, que no podia pasar
/// nunca: era una plantilla copiada de un entrypoint que si mapea. Si algun dia el servicio empieza a usar
/// Mapster, lo que hay que anadir son las reglas y su test, no una asercion que da por hecho que existen.
/// </para>
/// </remarks>
public class MapsterConfigTest
{
    [Fact]
    public void ConfigureDoesNotThrowAndTheMapperCanBeBuilt()
    {
        CodeDesignPlus.Net.Microservice.Licenses.gRpc.Core.Mapster.MapsterConfig.Configure();

        var mapper = new Mapper(TypeAdapterConfig.GlobalSettings);

        Assert.NotNull(mapper);
    }
}
