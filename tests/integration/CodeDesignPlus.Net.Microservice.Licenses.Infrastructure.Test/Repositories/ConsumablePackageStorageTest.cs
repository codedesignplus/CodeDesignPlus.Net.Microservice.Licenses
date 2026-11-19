using CodeDesignPlus.Net.ValueObjects.Location;
using CodeDesignPlus.Net.ValueObjects.User;
using NodaTime;
using License = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.License;
using LicenseModule = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.LicenseModule;
using Tenant = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.Tenant;

namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Test.Repositories;

/// <summary>
/// A consumable package and the order that adds to it against a real Mongo (pendings/744): the marks are read back, and the
/// packages and orders stored before they existed are still read (rule 31).
/// </summary>
[Collection(MongoContainerFixture.Collection)]
public class ConsumablePackageStorageTest
{
    private static readonly Guid SuperAdmin = Guid.Parse("10000000-0000-0000-0000-000000000001");

    private readonly IPackageRepository packages;
    private readonly IOrderRepository orders;
    private readonly IMongoDatabase database;

    public ConsumablePackageStorageTest(MongoContainerFixture fixture)
    {
        var name = $"db-ms-licenses-{Guid.NewGuid():N}";
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Mongo:Enable"] = "true",
                ["Mongo:ConnectionString"] = fixture.ConnectionString,
                ["Mongo:Database"] = name,
                ["Mongo:RegisterHealthCheck"] = "false",
            })
            .Build();

        var services = new ServiceCollection();

        services.AddLogging();
        services.AddMongo<Startup>(configuration);

        var provider = services.BuildServiceProvider();

        this.packages = provider.GetRequiredService<IPackageRepository>();
        this.orders = provider.GetRequiredService<IOrderRepository>();
        this.database = new MongoClient(fixture.ConnectionString).GetDatabase(name);
    }

    private static Money Cop(long minor) => Money.FromLong(minor, "COP");

    private static PackageAggregate Documents() => PackageAggregate.Create(Guid.NewGuid(), new PackageDefinition(
        "Documents", "Short", "Long", Icon.Create("document", "#16a34a"), true, false, true, [], PackageKind.Configurable, "COP",
        Cop(0), Cop(0), TaxTreatment.Taxed, 1900, null, "Concept", [],
        [
            new PackageComponent("documents", PackageComponentKind.Volume, AttributeCombination.Sum, 1, 3333, 1, 50, Cop(0), null, null, false, [new PackageTier(1, Cop(15_000)), new PackageTier(51, Cop(11_000))])
            {
                Multiplier = 12,
                Consumable = true
            },
        ]), SuperAdmin);

    private static Tenant LasAcacias(Guid id)
    {
        var currency = Currency.Create(Guid.NewGuid(), "Peso Colombiano", "COP", "$", 2, 170);
        var country = Country.Create(Guid.NewGuid(), "Colombia", "CO", "COL", 170, "+57", "America/Bogota", currency);
        var location = Location.Create(country, State.Create(Guid.NewGuid(), "Bogotá, D.C.", "11"), City.Create(Guid.NewGuid(), "Bogotá, D.C.", "America/Bogota"), null, null, "Carrera 7 # 71-21", "110111");

        return Tenant.Create(id, "Conjunto Las Acacias", null, TypeDocument.Create("NIT", "NIT"), "901888777-3", "+573001234567", "admin@acacias.com", location);
    }

    private static License Annual(long totalMinor) => License.Create(Guid.NewGuid(), "Estándar", Cop(totalMinor), Cop(0), Cop(totalMinor), BillingType.Annually, BillingModel.None, "Plan", "Plan", Icon.Create("solar:home-2-linear", "#2563eb"), "Terms", false, true, [], [new LicenseModule(Guid.NewGuid(), "Organización", "Identidad")]);

    /// <summary>
    /// The consumable package comes back consumable, and a package stored before the mark is read as not consumable.
    /// </summary>
    [Fact]
    public async Task FindAsync_AConsumablePackage_ReadsTheMarkBackAndToleratesItsAbsence()
    {
        // Arrange
        var package = Documents();

        await this.packages.CreateIfAbsentAsync(package, CancellationToken.None);

        var before = await this.packages.FindAsync<PackageAggregate>(package.Id, CancellationToken.None);
        var raw = this.database.GetCollection<BsonDocument>(nameof(PackageAggregate));
        var filter = Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(package.Id, GuidRepresentation.Standard));

        // Act
        var unset = await raw.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Unset("Components.$[].Consumable"));
        var after = await this.packages.FindAsync<PackageAggregate>(package.Id, CancellationToken.None);

        // Assert
        Assert.Equal((true, 1L, false, (long?)12), (before.IsConsumable, unset.ModifiedCount, after.IsConsumable, after.Components[0].Multiplier));
    }

    /// <summary>
    /// The paid order that adds to a package is read back with its mark, its total attributes and only what it charged; an
    /// order stored before the mark is read without it.
    /// </summary>
    [Fact]
    public async Task FindPaidWithPackagesByTenantAsync_AnOrderThatAddsToAPackage_ReadsTheMarkBackAndToleratesItsAbsence()
    {
        // Arrange
        var tenant = LasAcacias(Guid.NewGuid());
        var buyer = Buyer.CreateMinimal(Guid.NewGuid(), "Buyer", "+573001234567", "buyer@example.com");
        var package = Documents();
        var start = SystemClock.Instance.GetCurrentInstant().Minus(Duration.FromDays(30));

        var purchase = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid(), Annual(345_100_000), buyer, tenant, buyer.BuyerId,
            [PackagePricing.Quote(package, new Dictionary<string, string> { ["documents"] = "50" }).ToLine(Guid.NewGuid(), null)]);

        purchase.SetPaymentStatus(PaymentStatus.Succeeded, buyer.BuyerId, start);

        var held = purchase.Packages![0];
        var line = PackagePricing.QuoteAddition(package, new Dictionary<string, string> { ["documents"] = "100" }, held.Attributes).ToLine(Guid.NewGuid(), held.LineId);
        var addition = OrderAggregate.CreatePackagePurchase(Guid.NewGuid(), Guid.NewGuid(), Annual(0), buyer, tenant, line, [], SubscriptionCharge.Compute(line.Total, Cop(0), Cop(0)), buyer.BuyerId);

        addition.SetPaymentStatus(PaymentStatus.Succeeded, buyer.BuyerId, licensePeriodEnd: purchase.PeriodEnd, packagePeriodEnd: held.PeriodEnd);

        await this.orders.CreateAsync(purchase, CancellationToken.None);
        await this.orders.CreateAsync(addition, CancellationToken.None);

        var raw = this.database.GetCollection<BsonDocument>(nameof(OrderAggregate));
        var filter = Builders<BsonDocument>.Filter.Eq("_id", new BsonBinaryData(addition.Id, GuidRepresentation.Standard));

        // Act
        var before = PackageTerm.PaidLines(await this.orders.FindPaidWithPackagesByTenantAsync(tenant.Id, CancellationToken.None)).Single(x => x.Order.Id == addition.Id).Line;
        var unset = await raw.UpdateOneAsync(filter, Builders<BsonDocument>.Update.Unset("Packages.$[].AddsToReplaced"));
        var after = PackageTerm.PaidLines(await this.orders.FindPaidWithPackagesByTenantAsync(tenant.Id, CancellationToken.None)).Single(x => x.Order.Id == addition.Id).Line;

        // Assert
        Assert.Equal(
            ((bool?)true, "1200", 600 * 11_000L, (Guid?)held.LineId, held.PeriodEnd!.Value.ToUnixTimeMilliseconds(), 1L, (bool?)null, "1200"),
            (before.AddsToReplaced, before.Attributes[0].Value, before.SubTotal.Amount, before.ReplacesLineId, before.PeriodEnd!.Value.ToUnixTimeMilliseconds(), unset.ModifiedCount, after.AddsToReplaced, after.Attributes[0].Value));
    }
}
