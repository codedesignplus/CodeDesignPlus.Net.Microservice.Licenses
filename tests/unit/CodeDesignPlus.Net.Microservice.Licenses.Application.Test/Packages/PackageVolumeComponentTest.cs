using System.Collections.Generic;
using System.Linq;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Packages;

/// <summary>
/// La cantidad por tramos de volumen de un paquete configurable (pendings/744): lo elegido fija el tramo, el tramo fija el
/// precio de cada unidad, y lo que se entrega y se cobra es lo elegido por su multiplicador.
/// </summary>
public class PackageVolumeComponentTest
{
    private static Instant Now => SystemClock.Instance.GetCurrentInstant();

    private static Dictionary<string, string> Monthly(long value) => new() { ["documents"] = value.ToString(System.Globalization.CultureInfo.InvariantCulture) };

    /// <summary>
    /// Cada frontera de los cinco tramos: lo entregado es doce veces lo elegido, y todo ello va al precio del tramo en que
    /// cae lo elegido ($150, $110, $85, $65 y $50 en unidades mínimas).
    /// </summary>
    [Theory]
    [InlineData(1, 12, 15_000)]
    [InlineData(50, 600, 15_000)]
    [InlineData(51, 612, 11_000)]
    [InlineData(200, 2_400, 11_000)]
    [InlineData(201, 2_412, 8_500)]
    [InlineData(500, 6_000, 8_500)]
    [InlineData(501, 6_012, 6_500)]
    [InlineData(2000, 24_000, 6_500)]
    [InlineData(2001, 24_012, 5_000)]
    [InlineData(3333, 39_996, 5_000)]
    public void Quote_AVolumeQuantity_ChargesEveryDeliveredUnitAtThePriceOfItsTier(long chosen, long delivered, long unitPriceMinor)
    {
        // Act
        var quote = PackagePricing.Quote(PackageFixtures.Documents(), Monthly(chosen));
        var line = Assert.Single(quote.Lines);

        // Assert
        Assert.Equal((delivered, unitPriceMinor, delivered * unitPriceMinor), (line.Units, line.UnitPrice.Amount, quote.SubTotal.Amount));
    }

    /// <summary>
    /// El precio no se escalona: con 51 al mes las 612 unidades van a $110, no las primeras 600 a $150 y el resto a $110.
    /// </summary>
    [Fact]
    public void Quote_AVolumeQuantityInTheSecondTier_PricesTheWholeQuantityAtThatTier()
    {
        // Act
        var quote = PackagePricing.Quote(PackageFixtures.Documents(), Monthly(51));

        // Assert
        Assert.Equal(612 * 11_000L, quote.SubTotal.Amount);
    }

    /// <summary>
    /// El atributo lleva lo entregado (600), que es lo que suma al de la licencia; lo elegido se guarda como se eligió (50)
    /// para poder renovarlo y recotizarlo.
    /// </summary>
    [Fact]
    public void Quote_AVolumeQuantityWithAMultiplier_DeliversTheMultipleAndKeepsWhatWasChosen()
    {
        // Act
        var quote = PackagePricing.Quote(PackageFixtures.Documents(), Monthly(50));
        var attribute = Assert.Single(quote.Attributes);

        // Assert
        Assert.Equal(("documents", "600", AttributeCombination.Sum, "50", "50"), (attribute.Key, attribute.Value, attribute.Combination, quote.Selections["documents"], quote.Lines[0].Value));
    }

    /// <summary>
    /// Sin multiplicador se entrega lo elegido, una unidad por unidad.
    /// </summary>
    [Fact]
    public void Quote_AVolumeQuantityWithoutAMultiplier_DeliversWhatWasChosen()
    {
        // Act
        var quote = PackagePricing.Quote(PackageFixtures.Documents(multiplier: null), Monthly(51));

        // Assert
        Assert.Equal(("51", 51 * 11_000L), (quote.Attributes[0].Value, quote.SubTotal.Amount));
    }

    /// <summary>
    /// Sin elegir nada se cotiza el valor por defecto, que es lo que enseña la tarjeta del catálogo.
    /// </summary>
    [Fact]
    public void Quote_AVolumeQuantityWithNothingChosen_UsesItsDefault()
    {
        // Act
        var quote = PackagePricing.Quote(PackageFixtures.Documents(), null);

        // Assert
        Assert.Equal(("50", 600 * 15_000L, 600 * 15_000L * 19 / 100), (quote.Selections["documents"], quote.SubTotal.Amount, quote.Tax.Amount));
    }

    /// <summary>
    /// Como una cantidad, enciende su sí/no cuando lo elegido es mayor que cero.
    /// </summary>
    [Fact]
    public void Quote_AVolumeQuantityWithAnEnableKey_TurnsTheFlagOn()
    {
        // Act
        var quote = PackagePricing.Quote(PackageFixtures.Documents(enableKey: "documents_enable"), Monthly(10));

        // Assert
        Assert.Contains(quote.Attributes, x => x is { Key: "documents_enable", Value: "true", Combination: AttributeCombination.Or });
    }

    /// <summary>
    /// Lo elegido respeta el rango del componente: ni por debajo del mínimo, ni por encima del máximo, ni un texto.
    /// </summary>
    [Theory]
    [InlineData("0")]
    [InlineData("3334")]
    [InlineData("many")]
    public void Quote_AVolumeQuantityOutOfItsRange_IsRejected(string chosen)
    {
        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackagePricing.Quote(PackageFixtures.Documents(), new Dictionary<string, string> { ["documents"] = chosen }));

        // Assert
        Assert.Equal("255", exception.Code);
    }

    /// <summary>
    /// Con mínimo cero, elegir cero no entrega ni cobra nada por ese componente: no necesita tramo.
    /// </summary>
    [Fact]
    public void Quote_AVolumeQuantityAtZeroNextToAnotherComponent_ChargesNothingForIt()
    {
        // Arrange
        var package = PackageAggregate.Create(Guid.NewGuid(), PackageFixtures.Configurable(
            PackageFixtures.DocumentsComponent(min: 0, defaultValue: 0),
            new PackageComponent("seats", PackageComponentKind.Quantity, AttributeCombination.Sum, 1, 10, 1, 1, PackageFixtures.Cop(100_000), null, null, false, [])), Guid.NewGuid());

        // Act
        var quote = PackagePricing.Quote(package, null);

        // Assert
        Assert.Equal((0L, "0", 100_000L), (quote.Lines[0].Amount.Amount, quote.Attributes[0].Value, quote.SubTotal.Amount));
    }

    /// <summary>
    /// Los tramos de volumen tienen que poner precio a toda cantidad elegible: sin tramos, con un inicio en cero o repetido,
    /// con el primero por encima de la menor cantidad, con uno más allá del máximo o con precio unitario propio, no se guarda.
    /// </summary>
    [Theory]
    [InlineData("none")]
    [InlineData("zero")]
    [InlineData("repeated")]
    [InlineData("gap")]
    [InlineData("beyond")]
    [InlineData("unitPrice")]
    public void Create_AVolumeQuantityWhoseTiersDoNotPriceEveryQuantity_IsRejected(string scenario)
    {
        // Arrange
        var component = scenario switch
        {
            "none" => PackageFixtures.DocumentsComponent(tiers: []),
            "zero" => PackageFixtures.DocumentsComponent(tiers: [new PackageTier(0, PackageFixtures.Cop(15_000))]),
            "repeated" => PackageFixtures.DocumentsComponent(tiers: [new PackageTier(1, PackageFixtures.Cop(15_000)), new PackageTier(1, PackageFixtures.Cop(11_000))]),
            "gap" => PackageFixtures.DocumentsComponent(tiers: [new PackageTier(2, PackageFixtures.Cop(15_000))]),
            "beyond" => PackageFixtures.DocumentsComponent(tiers: [new PackageTier(1, PackageFixtures.Cop(15_000)), new PackageTier(3334, PackageFixtures.Cop(5_000))]),
            _ => PackageFixtures.DocumentsComponent(unitPriceMinor: 100)
        };

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackageAggregate.Create(Guid.NewGuid(), PackageFixtures.Configurable(component), Guid.NewGuid()));

        // Assert
        Assert.Equal("276", exception.Code);
    }

    /// <summary>
    /// Con mínimo cero el primer tramo puede empezar en la primera cantidad que se cobra, que es el paso.
    /// </summary>
    [Fact]
    public void Create_AVolumeQuantityFromZeroWhoseFirstTierStartsAtTheStep_IsAccepted()
    {
        // Act
        var package = PackageAggregate.Create(Guid.NewGuid(), PackageFixtures.Configurable(PackageFixtures.DocumentsComponent(min: 0)), Guid.NewGuid());

        // Assert
        Assert.Equal(5, package.Components[0].Tiers.Count);
    }

    /// <summary>
    /// El multiplicador es un entero mayor que cero que no desborda el máximo.
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(-12)]
    [InlineData(long.MaxValue)]
    public void Create_AVolumeQuantityWithAMultiplierOutOfBounds_IsRejected(long multiplier)
    {
        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackageFixtures.Documents(multiplier));

        // Assert
        Assert.Equal("277", exception.Code);
    }

    /// <summary>
    /// Solo la cantidad por tramos de volumen entrega un múltiplo: en una cantidad de precio fijo el multiplicador es un error.
    /// </summary>
    [Fact]
    public void Create_AQuantityWithAMultiplier_IsRejected()
    {
        // Arrange
        var quantity = new PackageComponent("seats", PackageComponentKind.Quantity, AttributeCombination.Sum, 1, 10, 1, 1, PackageFixtures.Cop(100_000), null, null, false, []) { Multiplier = 12 };

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackageAggregate.Create(Guid.NewGuid(), PackageFixtures.Configurable(quantity), Guid.NewGuid()));

        // Assert
        Assert.Equal("277", exception.Code);
    }

    /// <summary>
    /// La pantalla devuelve el 1 que leyó: en un componente que no es de volumen es lo mismo que nada y no se guarda.
    /// </summary>
    [Fact]
    public void Create_AQuantityWithAMultiplierOfOne_StoresNoMultiplier()
    {
        // Arrange
        var quantity = new PackageComponent("seats", PackageComponentKind.Quantity, AttributeCombination.Sum, 1, 10, 1, 1, PackageFixtures.Cop(100_000), null, null, false, []) { Multiplier = 1 };

        // Act
        var package = PackageAggregate.Create(Guid.NewGuid(), PackageFixtures.Configurable(quantity), Guid.NewGuid());

        // Assert
        Assert.Null(package.Components[0].Multiplier);
    }

    /// <summary>
    /// Una cantidad de precio fijo sigue sin admitir tramos: los tramos de volumen son de su propio tipo.
    /// </summary>
    [Fact]
    public void Create_AQuantityWithTiers_IsStillRejected()
    {
        // Arrange
        var quantity = new PackageComponent("seats", PackageComponentKind.Quantity, AttributeCombination.Sum, 1, 10, 1, 1, PackageFixtures.Cop(100_000), null, null, false, PackageFixtures.DocumentTiers());

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackageAggregate.Create(Guid.NewGuid(), PackageFixtures.Configurable(quantity), Guid.NewGuid()));

        // Assert
        Assert.Equal("255", exception.Code);
    }

    /// <summary>
    /// Un sí/no o un tramo se cobran por unidad de una cantidad de precio fijo; de una por tramos de volumen, no: no se sabría
    /// si por lo elegido o por lo entregado.
    /// </summary>
    [Fact]
    public void Create_APerUnitOfThatPointsToAVolumeQuantity_IsRejected()
    {
        // Arrange
        var toggle = new PackageComponent("priority", PackageComponentKind.Toggle, AttributeCombination.Or, 0, 0, 0, 0, PackageFixtures.Cop(100), "documents", null, false, []);

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackageAggregate.Create(Guid.NewGuid(), PackageFixtures.Configurable(PackageFixtures.DocumentsComponent(), toggle), Guid.NewGuid()));

        // Assert
        Assert.Equal("252", exception.Code);
    }

    /// <summary>
    /// Los tramos se guardan de menor a mayor aunque lleguen desordenados.
    /// </summary>
    [Fact]
    public void Create_AVolumeQuantityWithUnsortedTiers_StoresThemFromLowestToHighest()
    {
        // Arrange
        var tiers = PackageFixtures.DocumentTiers();

        tiers.Reverse();

        // Act
        var package = PackageAggregate.Create(Guid.NewGuid(), PackageFixtures.Configurable(PackageFixtures.DocumentsComponent(tiers: tiers)), Guid.NewGuid());

        // Assert
        Assert.Equal([1L, 51L, 201L, 501L, 2001L], package.Components[0].Tiers.Select(x => x.Value));
    }

    /// <summary>
    /// Un paquete guardado antes del multiplicador no trae ese elemento en sus componentes: se sigue leyendo y cotiza como
    /// siempre (regla 31).
    /// </summary>
    [Fact]
    public void Deserialize_APackageStoredBeforeTheMultiplier_StillReadsAndQuotesTheSame()
    {
        // Arrange
        CodeDesignPlus.Net.Mongo.Extensions.MongoSerializerRegistration.RegisterSerializers();

        var custom = PackageFixtures.Custom();
        var document = custom.ToBsonDocument();

        foreach (var component in document["Components"].AsBsonArray)
        {
            component.AsBsonDocument.Remove("Multiplier");
        }

        // Act
        var stored = BsonSerializer.Deserialize<PackageAggregate>(document);

        // Assert
        Assert.Equal((6, true, PackagePricing.Quote(custom, null).Total.Amount), (stored.Components.Count, stored.Components.All(x => x.Multiplier is null), PackagePricing.Quote(stored, null).Total.Amount));
    }

    /// <summary>
    /// El paquete con su cantidad por tramos de volumen vuelve de Mongo con su multiplicador y sus tramos.
    /// </summary>
    [Fact]
    public void Deserialize_APackageWithAVolumeQuantity_ReadsItsMultiplierAndTiersBack()
    {
        // Arrange
        CodeDesignPlus.Net.Mongo.Extensions.MongoSerializerRegistration.RegisterSerializers();

        // Act
        var stored = BsonSerializer.Deserialize<PackageAggregate>(PackageFixtures.Documents().ToBsonDocument());
        var component = Assert.Single(stored.Components);

        // Assert
        Assert.Equal((PackageComponentKind.Volume, (long?)12, 5, 600 * 15_000L), (component.Kind, component.Multiplier, component.Tiers.Count, PackagePricing.Quote(stored, null).SubTotal.Amount));
    }

    /// <summary>
    /// La pantalla lee el multiplicador y los tramos del componente; en los demás tipos el multiplicador es 1.
    /// </summary>
    [Fact]
    public void From_APackageWithAVolumeQuantity_ExposesItsMultiplierAndTiers()
    {
        // Act
        var documents = PackageDto.From(PackageFixtures.Documents()).Components[0];
        var custom = PackageDto.From(PackageFixtures.Custom()).Components;

        // Assert
        Assert.Equal((PackageComponentKind.Volume, 12L, 51L, 11_000L, true), (documents.Kind, documents.Multiplier, documents.Tiers[1].Value, documents.Tiers[1].Price, custom.All(x => x.Multiplier == 1)));
    }

    /// <summary>
    /// Lo que escribe el Super Admin llega con su multiplicador y los precios de los tramos en unidad mayor ($150 son 15.000
    /// unidades mínimas), leído con Newtonsoft como lo lee el REST; un cuerpo sin multiplicador, como los de antes, se sigue leyendo.
    /// </summary>
    [Fact]
    public void ToComponent_AVolumeQuantityWrittenAsJson_CarriesItsMultiplierAndConvertsItsTierPrices()
    {
        // Arrange
        const string body = """{"key":"documents","kind":3,"combine":0,"min":1,"max":3333,"step":1,"default":50,"unitPrice":0,"required":false,"tiers":[{"value":1,"price":150},{"value":51,"price":110}],"multiplier":12}""";

        // Act
        var component = Newtonsoft.Json.JsonConvert.DeserializeObject<PackageComponentInput>(body)!.ToComponent("COP", 2);
        var legacy = Newtonsoft.Json.JsonConvert.DeserializeObject<PackageComponentInput>(body.Replace(",\"multiplier\":12", string.Empty))!.ToComponent("COP", 2);

        // Assert
        Assert.Equal((PackageComponentKind.Volume, (long?)12, 15_000L, (long?)null), (component.Kind, component.Multiplier, component.Tiers[0].Price.Amount, legacy.Multiplier));
    }

    /// <summary>
    /// Ampliar el cupo es elegir una cifra mayor: el atributo sube, la línea nueva reemplaza a la anterior con el crédito de
    /// lo no usado, y la copropiedad queda con el cupo nuevo.
    /// </summary>
    [Fact]
    public void QuoteAddOn_RaisingAVolumeQuantity_IsAnExtensionThatDeliversTheNewQuota()
    {
        // Arrange
        var (state, order, documents) = Subscribed(50);

        // Act
        var quote = state.QuoteAddOn(documents, Monthly(100), Money.Zero("COP"), Now);

        // Assert
        Assert.Equal((order.Packages![0].LineId, "1200", 1_200 * 11_000L), (quote.ReplacesLineId, quote.Quote.Attributes[0].Value, quote.Quote.SubTotal.Amount));
    }

    /// <summary>
    /// Bajar la cifra a mitad de periodo no es ampliar: se hace al renovar.
    /// </summary>
    [Fact]
    public void QuoteAddOn_LoweringAVolumeQuantity_IsRejected()
    {
        // Arrange
        var (state, _, documents) = Subscribed(100);

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => state.QuoteAddOn(documents, Monthly(50), Money.Zero("COP"), Now));

        // Assert
        Assert.Equal("267", exception.Code);
    }

    private static (TenantPackages State, OrderAggregate Order, PackageAggregate Documents) Subscribed(long monthly)
    {
        var documents = PackageFixtures.Documents();
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid(), PackageFixtures.AnnualLicense(), CodeDesignPlus.Net.ValueObjects.User.Buyer.CreateMinimal(TestOrders.BuyerId, "Buyer", "+573001234567", "buyer@example.com"), TestOrders.BuildTenant(), TestOrders.BuyerId,
            [PackagePricing.Quote(documents, Monthly(monthly)).ToLine(Guid.NewGuid(), null)]);

        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, Now.Minus(Duration.FromDays(100)));

        return (new TenantPackages(SubscriptionTimeline.From([order])!, PackageTerm.PaidLines([order])), order, documents);
    }
}
