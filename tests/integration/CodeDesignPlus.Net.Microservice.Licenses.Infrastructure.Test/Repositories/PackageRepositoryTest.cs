namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Test.Repositories;

/// <summary>
/// The catalog of packages against a real Mongo (pendings/744): a quantity priced by volume tiers is read back with its
/// tiers and its multiplier, and the packages stored before the multiplier existed are still read (rule 31).
/// </summary>
[Collection(MongoContainerFixture.Collection)]
public class PackageRepositoryTest
{
    private static readonly Guid SuperAdmin = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private readonly IPackageRepository packages;
    private readonly IMongoCollection<BsonDocument> raw;

    public PackageRepositoryTest(MongoContainerFixture fixture)
    {
        var database = $"db-ms-licenses-{Guid.NewGuid():N}";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:Enable"] = "true",
                ["Mongo:ConnectionString"] = fixture.ConnectionString,
                ["Mongo:Database"] = database,
                ["Mongo:RegisterHealthCheck"] = "false",
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMongo<Startup>(configuration);

        this.packages = services.BuildServiceProvider().GetRequiredService<IPackageRepository>();
        this.raw = new MongoClient(fixture.ConnectionString).GetDatabase(database).GetCollection<BsonDocument>(nameof(PackageAggregate));
    }

    private static Money Cop(long minor) => Money.FromLong(minor, "COP");

    private static PackageDefinition Configurable(string name, params PackageComponent[] components) => new(
        name, "Short", "Long", Icon.Create("document", "#16a34a"), true, false, true, [], PackageKind.Configurable, "COP",
        Cop(0), Cop(0), TaxTreatment.Taxed, 1900, null, "Concept", [], [.. components]);

    private static PackageComponent Documents() => new("documents", PackageComponentKind.Volume, AttributeCombination.Sum, 1, 3333, 1, 50, Cop(0), null, null, false,
        [new PackageTier(1, Cop(15_000)), new PackageTier(51, Cop(11_000)), new PackageTier(201, Cop(8_500)), new PackageTier(501, Cop(6_500)), new PackageTier(2001, Cop(5_000))])
    {
        Multiplier = 12
    };

    private static PackageComponent Seats() => new("seats", PackageComponentKind.Quantity, AttributeCombination.Sum, 1, 10, 1, 2, Cop(100_000), null, "seats_enable", false, []);

    /// <summary>
    /// The package written by the repository comes back with the kind, the five tiers and the multiplier, and quotes the
    /// same as before it was stored: 200 a month are 2.400 documents at $110.
    /// </summary>
    [Fact]
    public async Task FindAsync_APackageWithAVolumeQuantity_ReadsBackItsTiersAndMultiplierAndQuotesTheSame()
    {
        // Arrange
        var package = PackageAggregate.Create(Guid.NewGuid(), Configurable("Documents", Documents()), SuperAdmin);

        await this.packages.CreateIfAbsentAsync(package, CancellationToken.None);

        // Act
        var stored = await this.packages.FindAsync<PackageAggregate>(package.Id, CancellationToken.None);
        var component = Assert.Single(stored.Components);
        var quote = PackagePricing.Quote(stored, new Dictionary<string, string> { ["documents"] = "200" });

        // Assert
        Assert.Equal(
            (PackageComponentKind.Volume, (long?)12, "1:15000,51:11000,201:8500,501:6500,2001:5000", "2400", 2_400 * 11_000L),
            (component.Kind, component.Multiplier, string.Join(",", component.Tiers.Select(x => $"{x.Value}:{x.Price.Amount}")), quote.Attributes[0].Value, quote.SubTotal.Amount));
    }

    /// <summary>
    /// A package stored before the multiplier does not carry that element in its components: the repository still reads
    /// it, with no multiplier, and it quotes as it always did.
    /// </summary>
    [Fact]
    public async Task FindAsync_APackageStoredBeforeTheMultiplier_IsStillRead()
    {
        // Arrange
        var package = PackageAggregate.Create(Guid.NewGuid(), Configurable("Seats", Seats()), SuperAdmin);

        await this.packages.CreateIfAbsentAsync(package, CancellationToken.None);

        var filter = Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(package.Id, GuidRepresentation.Standard));
        var unset = await this.raw.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Unset("Components.$[].Multiplier"));
        var document = await this.raw.Find(filter).SingleAsync();

        // Act
        var stored = await this.packages.FindAsync<PackageAggregate>(package.Id, CancellationToken.None);

        // Assert
        Assert.Equal(
            (1L, false, (long?)null, 2 * 100_000L),
            (unset.MatchedCount, document["Components"][0].AsBsonDocument.Contains("Multiplier"), stored.Components[0].Multiplier, PackagePricing.Quote(stored, null).SubTotal.Amount));
    }

    /// <summary>
    /// Changing the tiers of a stored package replaces them: the next quote uses the new prices.
    /// </summary>
    [Fact]
    public async Task UpdateAsync_ChangingTheVolumeTiers_QuotesWithTheNewPrices()
    {
        // Arrange
        var package = PackageAggregate.Create(Guid.NewGuid(), Configurable("Documents", Documents()), SuperAdmin);

        await this.packages.CreateIfAbsentAsync(package, CancellationToken.None);

        var cheaper = Documents() with { Tiers = [new PackageTier(1, Cop(14_000)), new PackageTier(101, Cop(9_000))] };

        package.Update(Configurable("Documents", cheaper), SuperAdmin);

        // Act
        await this.packages.UpdateAsync(package, CancellationToken.None);

        var stored = await this.packages.FindAsync<PackageAggregate>(package.Id, CancellationToken.None);

        // Assert
        Assert.Equal((2, 1_200 * 14_000L), (stored.Components[0].Tiers.Count, PackagePricing.Quote(stored, new Dictionary<string, string> { ["documents"] = "100" }).SubTotal.Amount));
    }
}
