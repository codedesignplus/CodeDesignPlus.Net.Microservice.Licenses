using System.Reflection;
using System.Text.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;
using System.Text.Json.Serialization;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Seeds;

/// <summary>
/// Siembra los paquetes complementarios del país del despliegue al arrancar el worker (pendings/739).
/// </summary>
/// <remarks>
/// <para>
/// <b>Un despliegue, un país.</b> El país sale de <c>Seed:Country</c> y elige el fichero embebido
/// <c>seed-&lt;país&gt;-packages.json</c>, como los tipos de documento de ms-catalogs. Solo el worker lo configura: la API y
/// el gRPC no siembran. Si el país está configurado y su fichero no existe, el worker no arranca.
/// </para>
/// <para>
/// <b>Se siembra por id y nunca se toca lo que ya existe</b> (regla 25): el Super Admin cambia precios y cantidades desde su
/// pantalla, y el arranque no se los devuelve a como venían. Un paquete sembrado que se borra no vuelve: se guarda borrado.
/// </para>
/// <para>
/// Los importes del fichero van en unidades mínimas de su moneda (<c>…Minor</c>): el arranque no depende de ms-locations.
/// </para>
/// </remarks>
public class PackageSeedService(IServiceScopeFactory scopeFactory, IConfiguration configuration, ILogger<PackageSeedService> logger) : BackgroundService
{
    /// <summary>
    /// El usuario del sistema que firma lo sembrado (regla 64).
    /// </summary>
    public static readonly Guid SystemUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    /// <inheritdoc/>
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var country = configuration["Seed:Country"];

        if (string.IsNullOrWhiteSpace(country))
        {
            return;
        }

        // Fuera del try a propósito: un país sin fichero debe tumbar el arranque, no quedarse en un aviso del log.
        var data = Load(country);

        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

        try
        {
            using var scope = scopeFactory.CreateScope();

            var repository = scope.ServiceProvider.GetRequiredService<IPackageRepository>();

            await SeedAsync(repository, data, stoppingToken);
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Error seeding the packages of {Country}.", country);
        }
    }

    /// <summary>
    /// Lee los paquetes del país. Falla si no existe su fichero.
    /// </summary>
    /// <param name="country">El país, p. ej. <c>co</c>.</param>
    /// <returns>Los paquetes del fichero.</returns>
    public static List<PackageSeed> Load(string country)
    {
        var fileName = $"seed-{country.Trim().ToLowerInvariant()}-packages.json";
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(fileName, StringComparison.Ordinal))
            ?? throw new InvalidOperationException($"No existe {fileName}: el país '{country}' no tiene paquetes para sembrar.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;

        return JsonSerializer.Deserialize<List<PackageSeed>>(stream, Options)!;
    }

    /// <summary>
    /// Crea los paquetes del fichero que falten, sin tocar los que ya existen.
    /// </summary>
    /// <param name="repository">El catálogo.</param>
    /// <param name="data">Los paquetes del fichero.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Cuántos creó.</returns>
    public async Task<int> SeedAsync(IPackageRepository repository, List<PackageSeed> data, CancellationToken cancellationToken)
    {
        var inserted = 0;

        foreach (var item in data)
        {
            try
            {
                if (await repository.ExistsAsync<PackageAggregate>(item.Id, cancellationToken))
                {
                    continue;
                }

                if (await repository.CreateIfAbsentAsync(PackageAggregate.Create(item.Id, item.ToDefinition(), SystemUserId), cancellationToken))
                {
                    inserted++;
                }
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "The package {Name} is not seeded.", item.Name);
            }
        }

        logger.LogInformation("Seeded {Inserted}/{Total} packages.", inserted, data.Count);

        return inserted;
    }
}

/// <summary>
/// Un paquete del fichero de siembra.
/// </summary>
public sealed record PackageSeed(Guid Id, string Name, string ShortDescription, string Description, string IconName, string IconColor, bool IsPopular, PackageKind Kind, string Currency, long BasePriceMinor, long MinimumPriceMinor, TaxTreatment TaxTreatment, int TaxBasisPoints, string? TaxLegalBasis, string InvoiceConcept, List<PackageAttributeSeed>? Attributes, List<PackageComponentSeed>? Components)
{
    /// <summary>
    /// Lo que define el paquete: público y activo.
    /// </summary>
    /// <returns>La definición.</returns>
    public PackageDefinition ToDefinition() => new(
        Name,
        ShortDescription,
        Description,
        Icon.Create(IconName, IconColor),
        true,
        IsPopular,
        true,
        [],
        Kind,
        Currency,
        Money.FromLong(BasePriceMinor, Currency),
        Money.FromLong(MinimumPriceMinor, Currency),
        TaxTreatment,
        TaxBasisPoints,
        TaxLegalBasis,
        InvoiceConcept,
        [.. (Attributes ?? []).Select(x => new PackageAttribute(x.Key, x.Value, x.Combine))],
        [.. (Components ?? []).Select(x => new PackageComponent(x.Key, x.Kind, x.Combine, x.Min, x.Max, x.Step, x.Default, Money.FromLong(x.UnitPriceMinor, Currency), x.PerUnitOf, x.EnableKey, x.Required, [.. (x.Tiers ?? []).Select(t => new PackageTier(t.Value, Money.FromLong(t.PriceMinor, Currency)))]))]);
}

/// <summary>
/// Un atributo de un paquete fijo del fichero.
/// </summary>
public sealed record PackageAttributeSeed(string Key, string Value, AttributeCombination Combine);

/// <summary>
/// Un componente de un paquete configurable del fichero.
/// </summary>
public sealed record PackageComponentSeed(string Key, PackageComponentKind Kind, AttributeCombination Combine, long Min, long Max, long Step, long Default, long UnitPriceMinor, string? PerUnitOf, string? EnableKey, bool Required, List<PackageTierSeed>? Tiers);

/// <summary>
/// Un tramo del fichero.
/// </summary>
public sealed record PackageTierSeed(long Value, long PriceMinor);
