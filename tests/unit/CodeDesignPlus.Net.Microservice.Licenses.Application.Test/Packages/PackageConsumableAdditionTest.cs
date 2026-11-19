using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.FileStorage;
using CodeDesignPlus.Net.Microservice.Emails.gRpc;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;
using LicenseSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.License;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Packages;

/// <summary>
/// Ampliar un paquete consumible es sumarle unidades (pendings/744): se cobra solo lo añadido, al precio del tramo en que
/// queda el total, sin crédito por tiempo, y lo añadido vence con lo que ya se tenía.
/// </summary>
public class PackageConsumableAdditionTest
{
    private static Instant Now => SystemClock.Instance.GetCurrentInstant();

    private static Dictionary<string, string> Monthly(long value) => new() { ["documents"] = value.ToString(System.Globalization.CultureInfo.InvariantCulture) };

    private static List<PackageAttribute> Held(long units) => [new PackageAttribute("documents", units.ToString(System.Globalization.CultureInfo.InvariantCulture), AttributeCombination.Sum)];

    private static PackageAggregate Consumable(long? multiplier = 12, string? enableKey = null) => PackageFixtures.Documents(multiplier, enableKey, consumable: true);

    private static CodeDesignPlus.Net.ValueObjects.User.Buyer Buyer() => CodeDesignPlus.Net.ValueObjects.User.Buyer.CreateMinimal(TestOrders.BuyerId, "Buyer", "+573001234567", "buyer@example.com");

    /// <summary>
    /// A tenant that bought the package <paramref name="daysAgo"/> days ago with the given monthly figure.
    /// </summary>
    private static (TenantPackages State, OrderAggregate Order, PackageAggregate Package) Subscribed(PackageAggregate package, long monthly, LicenseSnapshot? license = null, int daysAgo = 100)
    {
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid(), license ?? PackageFixtures.AnnualLicense(), Buyer(), TestOrders.BuildTenant(), TestOrders.BuyerId,
            [PackagePricing.Quote(package, Monthly(monthly)).ToLine(Guid.NewGuid(), null)]);

        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, Now.Minus(Duration.FromDays(daysAgo)));
        order.GetAndClearEvents();

        return (new TenantPackages(SubscriptionTimeline.From([order])!, PackageTerm.PaidLines([order])), order, package);
    }

    /// <summary>
    /// The order that adds to the package, still waiting for its payment.
    /// </summary>
    private static OrderAggregate AdditionOrder(TenantPackages state, PackageAggregate package, long monthly)
    {
        var quote = state.QuoteAddOn(package, Monthly(monthly), Money.Zero("COP"), Now);
        var order = OrderAggregate.CreatePackagePurchase(Guid.NewGuid(), Guid.NewGuid(), state.ReferenceLicense(Now), Buyer(), TestOrders.BuildTenant(), quote.Quote.ToLine(Guid.NewGuid(), quote.ReplacesLineId), quote.CreditedPeriods, quote.Charge, TestOrders.BuyerId);

        order.GetAndClearEvents();

        return order;
    }

    /// <summary>
    /// Con 600 documentos ya comprados, el total nuevo fija el tramo y se cobra solo la diferencia, toda al precio de ese
    /// tramo: pasar a 51 al mes son 12 más a $110; a 100, 600 más a $110; a 250, 2.400 más a $85; a 600, 6.600 más a $65.
    /// </summary>
    [Theory]
    [InlineData(51, 12, 11_000)]
    [InlineData(100, 600, 11_000)]
    [InlineData(250, 2_400, 8_500)]
    [InlineData(600, 6_600, 6_500)]
    public void QuoteAddition_ALargerTotal_ChargesOnlyTheAddedUnitsAtThePriceOfTheTierOfTheTotal(long total, long added, long unitPriceMinor)
    {
        // Act
        var quote = PackagePricing.QuoteAddition(Consumable(), Monthly(total), Held(600));
        var line = Assert.Single(quote.Lines);

        // Assert
        Assert.Equal((added, unitPriceMinor, added * unitPriceMinor, true), (line.Units, line.UnitPrice.Amount, quote.SubTotal.Amount, quote.IsAddition));
    }

    /// <summary>
    /// La línea que resulta lleva el total: el atributo sube en lo añadido (600 + 600 = 1.200) y lo elegido es la cifra
    /// total (100), que es de la que parte la renovación.
    /// </summary>
    [Fact]
    public void QuoteAddition_ALargerTotal_CarriesTheAttributesAndTheSelectionOfTheTotal()
    {
        // Act
        var quote = PackagePricing.QuoteAddition(Consumable(enableKey: "documents_enable"), Monthly(100), Held(600));
        var attributes = quote.Attributes.ToDictionary(x => x.Key, x => x.Value);

        // Assert
        Assert.Equal(("1200", "true", "100", "100"), (attributes["documents"], attributes["documents_enable"], quote.Selections["documents"], quote.Lines[0].Value));
    }

    /// <summary>
    /// Lo añadido lleva su impuesto con la tarifa del paquete, y nada más: ni la base ni el mínimo del paquete, que se
    /// cobraron al comprarlo.
    /// </summary>
    [Fact]
    public void QuoteAddition_APackageWithBaseAndMinimumPrice_ChargesNeitherAgain()
    {
        // Arrange
        var definition = PackageFixtures.Configurable(PackageFixtures.DocumentsComponent(consumable: true)) with { BasePrice = PackageFixtures.Cop(5_000_000), MinimumPrice = PackageFixtures.Cop(900_000_000) };
        var package = PackageAggregate.Create(Guid.NewGuid(), definition, Guid.NewGuid());

        // Act
        var quote = PackagePricing.QuoteAddition(package, Monthly(100), Held(600));

        // Assert
        Assert.Equal((600 * 11_000L, 600 * 11_000L * 19 / 100, 0L, false), (quote.SubTotal.Amount, quote.Tax.Amount, quote.BasePrice.Amount, quote.MinimumApplied));
    }

    /// <summary>
    /// Lo que ya se tiene sale de los atributos de la línea vigente, no de recalcular lo que se eligió: si el catálogo cambió
    /// el multiplicador a 10, quien tiene 600 y pasa a 100 al mes recibe 1.000 y paga 400.
    /// </summary>
    [Fact]
    public void QuoteAddition_TheCatalogChangedItsMultiplier_SubtractsWhatTheLineActuallyDelivers()
    {
        // Act
        var quote = PackagePricing.QuoteAddition(Consumable(multiplier: 10), Monthly(100), Held(600));

        // Assert
        Assert.Equal((400L, "1000"), (quote.Lines[0].Units, quote.Attributes[0].Value));
    }

    /// <summary>
    /// Sumar exige que el total suba: el mismo total no añade nada y uno menor quitaría unidades ya pagadas.
    /// </summary>
    [Theory]
    [InlineData(50)]
    [InlineData(49)]
    public void QuoteAddition_ATotalThatAddsNothing_IsRejected(long total)
    {
        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackagePricing.QuoteAddition(Consumable(), Monthly(total), Held(600)));

        // Assert
        Assert.Equal("280", exception.Code);
    }

    /// <summary>
    /// A un paquete que no es consumible no se le suma: su ampliación cobra el año y acredita lo no usado.
    /// </summary>
    [Fact]
    public void QuoteAddition_APackageThatIsNotConsumable_IsRejected()
    {
        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackagePricing.QuoteAddition(PackageFixtures.Documents(), Monthly(100), Held(600)));

        // Assert
        Assert.Equal("280", exception.Code);
    }

    /// <summary>
    /// El total nuevo respeta el rango del componente como cualquier elección.
    /// </summary>
    [Fact]
    public void QuoteAddition_ATotalAboveTheMaximum_IsRejected()
    {
        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackagePricing.QuoteAddition(Consumable(), Monthly(3334), Held(600)));

        // Assert
        Assert.Equal("255", exception.Code);
    }

    /// <summary>
    /// La línea de lo añadido dice que se sumó a la que reemplaza; la de una compra o una ampliación corriente, no.
    /// </summary>
    [Fact]
    public void ToLine_AnAddition_MarksTheLineAsAddedToTheOneItReplaces()
    {
        // Arrange
        var replaced = Guid.NewGuid();

        // Act
        var addition = PackagePricing.QuoteAddition(Consumable(), Monthly(100), Held(600)).ToLine(Guid.NewGuid(), replaced);
        var purchase = PackagePricing.Quote(Consumable(), Monthly(100)).ToLine(Guid.NewGuid(), replaced);

        // Assert
        Assert.Equal(((bool?)true, (bool?)null, 600 * 11_000L), (addition.AddsToReplaced, purchase.AddsToReplaced, addition.SubTotal.Amount));
    }

    /// <summary>
    /// Solo una cantidad por tramos de volumen es consumible.
    /// </summary>
    [Fact]
    public void Create_AConsumableThatIsNotAVolumeQuantity_IsRejected()
    {
        // Arrange
        var quantity = new PackageComponent("seats", PackageComponentKind.Quantity, AttributeCombination.Sum, 1, 10, 1, 1, PackageFixtures.Cop(100_000), null, null, false, []) { Consumable = true };

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackageAggregate.Create(Guid.NewGuid(), PackageFixtures.Configurable(quantity), Guid.NewGuid()));

        // Assert
        Assert.Equal("278", exception.Code);
    }

    /// <summary>
    /// Lo consumible se amplía sumando: un atributo del que se toma el mayor no puede serlo.
    /// </summary>
    [Fact]
    public void Create_AConsumableWhoseAttributeIsNotSummed_IsRejected()
    {
        // Arrange
        var component = PackageFixtures.DocumentsComponent(consumable: true) with { Combination = AttributeCombination.Max };

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackageAggregate.Create(Guid.NewGuid(), PackageFixtures.Configurable(component), Guid.NewGuid()));

        // Assert
        Assert.Equal("278", exception.Code);
    }

    /// <summary>
    /// Un paquete no mezcla consumibles con lo demás: una misma ampliación no puede sumar unas cosas y prorratear otras.
    /// </summary>
    [Fact]
    public void Create_APackageThatMixesConsumableWithOtherComponents_IsRejected()
    {
        // Arrange
        var seats = new PackageComponent("seats", PackageComponentKind.Quantity, AttributeCombination.Sum, 1, 10, 1, 1, PackageFixtures.Cop(100_000), null, null, false, []);

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackageAggregate.Create(Guid.NewGuid(), PackageFixtures.Configurable(PackageFixtures.DocumentsComponent(consumable: true), seats), Guid.NewGuid()));

        // Assert
        Assert.Equal("279", exception.Code);
    }

    /// <summary>
    /// Un paquete es consumible cuando lo son sus componentes; «no» se guarda como ausente, igual que en los ya guardados.
    /// </summary>
    [Fact]
    public void Create_ConsumableOrNot_SaysWhetherThePackageIsConsumable()
    {
        // Act
        var consumable = Consumable();
        var plain = PackageFixtures.Documents(consumable: false);

        // Assert
        Assert.Equal((true, (bool?)true, false, (bool?)null), (consumable.IsConsumable, consumable.Components[0].Consumable, plain.IsConsumable, plain.Components[0].Consumable));
    }

    /// <summary>
    /// Quien ya tiene el paquete y sube de 50 a 100 al mes paga solo 600 documentos a $110 más IVA: sin crédito por los cien
    /// días transcurridos, reemplazando su línea, y con el mismo vencimiento.
    /// </summary>
    [Fact]
    public void QuoteAddOn_RaisingAConsumablePackage_ChargesOnlyWhatIsAddedWithNoCreditAndTheSameEnd()
    {
        // Arrange
        var (state, order, package) = Subscribed(Consumable(), 50);
        var held = order.Packages![0];

        // Act
        var quote = state.QuoteAddOn(package, Monthly(100), Money.Zero("COP"), Now);

        // Assert
        Assert.Equal(
            ((Guid?)held.LineId, 0, 0L, 600 * 11_000L * 119 / 100, "1200", held.PeriodEnd),
            (quote.ReplacesLineId, quote.CreditedPeriods.Count, quote.Charge.ProrationCredit.Amount, quote.Charge.AmountDue.Amount, quote.Quote.Attributes[0].Value, (Instant?)quote.PeriodEnd));
    }

    /// <summary>
    /// Con licencia mensual el paquete dura un año desde su compra: la cotización de lo añadido anuncia ese mismo fin, no un
    /// año desde hoy.
    /// </summary>
    [Fact]
    public void QuoteAddOn_RaisingAConsumablePackageWithAMonthlyLicense_AnnouncesTheEndOfThePackageItAddsTo()
    {
        // Arrange
        var (state, order, package) = Subscribed(Consumable(), 50, TestOrders.BuildLicense(), daysAgo: 10);

        // Act
        var quote = state.QuoteAddOn(package, Monthly(100), Money.Zero("COP"), Now);

        // Assert
        Assert.Equal(order.Packages![0].PeriodEnd, (Instant?)quote.PeriodEnd);
    }

    /// <summary>
    /// La primera compra de un consumible es una compra corriente: el paquete entero, sin marca de suma.
    /// </summary>
    [Fact]
    public void QuoteAddOn_BuyingAConsumablePackageForTheFirstTime_ChargesTheWholePackage()
    {
        // Arrange
        var order = PackageFixtures.PaidPurchase(Now.Minus(Duration.FromDays(100)), PackageFixtures.AnnualLicense());
        var state = new TenantPackages(SubscriptionTimeline.From([order])!, []);

        // Act
        var quote = state.QuoteAddOn(Consumable(), Monthly(50), Money.Zero("COP"), Now);

        // Assert
        Assert.Equal((false, (Guid?)null, 600 * 15_000L), (quote.Quote.IsAddition, quote.ReplacesLineId, quote.Quote.SubTotal.Amount));
    }

    /// <summary>
    /// Bajar la cifra de un consumible a mitad de periodo tampoco es ampliar.
    /// </summary>
    [Fact]
    public void QuoteAddOn_LoweringAConsumablePackage_IsRejected()
    {
        // Arrange
        var (state, _, package) = Subscribed(Consumable(), 100);

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => state.QuoteAddOn(package, Monthly(50), Money.Zero("COP"), Now));

        // Assert
        Assert.Equal("267", exception.Code);
    }

    /// <summary>
    /// La misma cantidad por tramos de volumen sin la marca de consumible se sigue ampliando como siempre: el paquete
    /// entero con crédito por lo no usado.
    /// </summary>
    [Fact]
    public void QuoteAddOn_RaisingAVolumeQuantityThatIsNotConsumable_StillChargesTheYearAndCreditsTheUnusedPart()
    {
        // Arrange
        var (state, _, package) = Subscribed(PackageFixtures.Documents(), 50);

        // Act
        var quote = state.QuoteAddOn(package, Monthly(100), Money.Zero("COP"), Now);

        // Assert
        Assert.Equal((false, 1, 1_200 * 11_000L, true), (quote.Quote.IsAddition, quote.CreditedPeriods.Count, quote.Quote.SubTotal.Amount, quote.Charge.ProrationCredit.Amount > 0));
    }

    /// <summary>
    /// La cotización que ve la pantalla dice que es una suma y trae solo lo añadido.
    /// </summary>
    [Fact]
    public void ToDto_AnAddition_TellsTheScreenItIsOneAndWhatItAdds()
    {
        // Arrange
        var (state, _, package) = Subscribed(Consumable(), 50);

        // Act
        var dto = state.QuoteAddOn(package, Monthly(100), Money.Zero("COP"), Now).ToDto();

        // Assert
        Assert.Equal((true, 600L, 11_000L, 0L, "100"), (dto.IsAddition, dto.Lines[0].Units, dto.Lines[0].UnitPrice, dto.Credit, dto.Selections["documents"]));
    }

    /// <summary>
    /// Pagada con licencia mensual, lo añadido vence con el paquete que ya se tenía (un año desde su compra), no un año
    /// después de ampliarlo.
    /// </summary>
    [Fact]
    public void SetPaymentStatus_AnAdditionWithAMonthlyLicense_EndsWithThePackageItAddsTo()
    {
        // Arrange
        var (state, purchase, package) = Subscribed(Consumable(), 50, TestOrders.BuildLicense(), daysAgo: 10);
        var order = AdditionOrder(state, package, 100);
        var end = purchase.Packages![0].PeriodEnd;

        // Act
        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, packagePeriodEnd: end);

        // Assert
        Assert.Equal((end, (bool?)true), (order.Packages![0].PeriodEnd, order.Packages[0].AddsToReplaced));
    }

    /// <summary>
    /// El fin del paquete solo manda en una orden que suma: una compra corriente de paquete lo ignora, y también una suma
    /// cuyo paquete ya venció.
    /// </summary>
    [Fact]
    public void SetPaymentStatus_APackagePeriodEndThatDoesNotApply_IsIgnored()
    {
        // Arrange
        var purchase = PackageFixtures.PackageOrder(BillingType.Monthly);
        var (state, _, package) = Subscribed(Consumable(), 50, TestOrders.BuildLicense(), daysAgo: 10);
        var late = AdditionOrder(state, package, 100);
        var now = Now;

        // Act
        purchase.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, now, packagePeriodEnd: now.Plus(Duration.FromDays(30)));
        late.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, now, packagePeriodEnd: now.Minus(Duration.FromDays(1)));

        // Assert
        Assert.Equal((true, true), (purchase.Packages![0].PeriodEnd > now.Plus(Duration.FromDays(300)), late.Packages![0].PeriodEnd > now.Plus(Duration.FromDays(300))));
    }

    /// <summary>
    /// Al aprobarse el pago, el handler busca el paquete al que se suma y le pasa su fin a la orden.
    /// </summary>
    [Fact]
    public async Task Handle_AnAdditionPaid_EndsWithThePackageItAddsTo()
    {
        // Arrange
        var (state, purchase, package) = Subscribed(Consumable(), 50, TestOrders.BuildLicense(), daysAgo: 10);
        var order = AdditionOrder(state, package, 100);
        var repository = new Mock<IOrderRepository>();

        repository.Setup(x => x.FindAsync<OrderAggregate>(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        repository.Setup(x => x.FindPaidWithPackagesByTenantAsync(order.TenantDetail.Id, It.IsAny<CancellationToken>())).ReturnsAsync([purchase]);
        repository.Setup(x => x.ApplyPaymentStatusAsync(order, PaymentStatus.Initiated, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = new UpdateStateOrderCommandHandler(repository.Object, Mock.Of<IPubSub>(), Mock.Of<ILiveChannelGrpc>(), Mock.Of<IEmailGrpc>(), Mock.Of<IFileStorageGrpc>(), Mock.Of<ICurrencyGrpc>(), Mock.Of<ISubscriptionSettlement>(), Mock.Of<IPurchaseEmails>(), Mock.Of<ILogger<UpdateStateOrderCommandHandler>>());

        // Act
        await handler.Handle(new UpdateStateOrderCommand(order.Id, PaymentStatus.Succeeded), CancellationToken.None);

        // Assert
        Assert.Equal(purchase.Packages![0].PeriodEnd, order.Packages![0].PeriodEnd);
    }

    /// <summary>
    /// Pagada la suma, la copropiedad cuenta con el total y no con el total más lo anterior: la línea nueva reemplaza a la
    /// que tenía.
    /// </summary>
    [Fact]
    public void EffectiveAttributes_AfterAnAddition_CountTheTotalOnce()
    {
        // Arrange
        var (state, purchase, addition, _) = Extended();

        // Act
        var attributes = state.EffectiveAttributes(Now);

        // Assert
        Assert.Equal(("1200", 2), (attributes["documents"], PackageTerm.PaidLines([purchase, addition]).Count));
    }

    /// <summary>
    /// La renovación parte de la cifra total: renueva 100 al mes al precio del catálogo, el paquete entero.
    /// </summary>
    [Fact]
    public async Task RenewalQuotesAsync_AfterAnAddition_RenewsTheTotalAtItsFullPrice()
    {
        // Arrange
        var (state, _, addition, package) = Extended();
        var packages = new Mock<IPackageRepository>();

        packages.Setup(x => x.FindAsync<PackageAggregate>(package.Id, It.IsAny<CancellationToken>())).ReturnsAsync(package);

        // Act
        var renewed = Assert.Single(await state.RenewalQuotesAsync(packages.Object, null, Now, CancellationToken.None));

        // Assert
        Assert.Equal(((Guid?)addition.Packages![0].LineId, false, 1_200 * 11_000L, "100"), (renewed.Replaces, renewed.Quote.IsAddition, renewed.Quote.SubTotal.Amount, renewed.Quote.Selections["documents"]));
    }

    /// <summary>
    /// Un cambio de licencia recotiza el paquete entero para el periodo nuevo; como la línea vigente solo cobró lo añadido,
    /// se acredita lo no usado de ella y de la línea a la que se sumó, cada una contra su orden.
    /// </summary>
    [Fact]
    public async Task ChangeLinesAsync_AfterAnAddition_CreditsEveryLineThatWasPaidForTheQuota()
    {
        // Arrange
        var (state, purchase, addition, package) = Extended();
        var packages = new Mock<IPackageRepository>();

        packages.Setup(x => x.FindAsync<PackageAggregate>(package.Id, It.IsAny<CancellationToken>())).ReturnsAsync(package);

        // Act
        var (quotes, lines, credited) = await state.ChangeLinesAsync(packages.Object, Now, CancellationToken.None);

        // Assert
        Assert.Equal(
            (1_200 * 11_000L, (Guid?)addition.Packages![0].LineId, 2, true),
            (Assert.Single(quotes).SubTotal.Amount, Assert.Single(lines).ReplacesLineId, credited.Count, credited.Select(x => x.OrderId).ToHashSet().SetEquals([purchase.Id, addition.Id])));
    }

    /// <summary>
    /// Una orden guardada antes de las sumas no trae la marca en sus líneas: se sigue leyendo, sin marca (regla 31).
    /// </summary>
    [Fact]
    public void Deserialize_AnOrderStoredBeforeAdditions_StillReadsItsPackageLines()
    {
        // Arrange
        CodeDesignPlus.Net.Mongo.Extensions.MongoSerializerRegistration.RegisterSerializers();

        var (_, _, addition, _) = Extended();
        var document = addition.ToBsonDocument();

        // Act
        var kept = BsonSerializer.Deserialize<OrderAggregate>(document.DeepClone().AsBsonDocument);

        document["Packages"][0].AsBsonDocument.Remove("AddsToReplaced");

        var stored = BsonSerializer.Deserialize<OrderAggregate>(document);

        // Assert
        Assert.Equal(((bool?)true, (bool?)null, addition.Packages![0].Total.Amount), (kept.Packages![0].AddsToReplaced, stored.Packages![0].AddsToReplaced, stored.Packages[0].Total.Amount));
    }

    /// <summary>
    /// Un paquete guardado antes de la marca de consumible no la trae en sus componentes: se sigue leyendo y no lo es.
    /// </summary>
    [Fact]
    public void Deserialize_APackageStoredBeforeConsumables_StillReadsAndIsNotConsumable()
    {
        // Arrange
        CodeDesignPlus.Net.Mongo.Extensions.MongoSerializerRegistration.RegisterSerializers();

        var document = Consumable().ToBsonDocument();
        var kept = BsonSerializer.Deserialize<PackageAggregate>(document.DeepClone().AsBsonDocument);

        document["Components"][0].AsBsonDocument.Remove("Consumable");

        // Act
        var stored = BsonSerializer.Deserialize<PackageAggregate>(document);

        // Assert
        Assert.Equal((true, false, (long?)12), (kept.IsConsumable, stored.IsConsumable, stored.Components[0].Multiplier));
    }

    /// <summary>
    /// La pantalla lee si el paquete y su componente son consumibles, y el Super Admin lo escribe en el componente; un
    /// cuerpo sin el campo, como los de antes, vale «no».
    /// </summary>
    [Fact]
    public void Dto_AConsumableComponent_IsReadAndWritten()
    {
        // Arrange
        const string body = """{"key":"documents","kind":3,"combine":0,"min":1,"max":3333,"step":1,"default":50,"unitPrice":0,"required":false,"tiers":[{"value":1,"price":150}],"multiplier":12,"consumable":true}""";

        // Act
        var read = PackageDto.From(Consumable());
        var plain = PackageDto.From(PackageFixtures.Documents());
        var written = Newtonsoft.Json.JsonConvert.DeserializeObject<PackageComponentInput>(body)!.ToComponent("COP", 2);
        var legacy = Newtonsoft.Json.JsonConvert.DeserializeObject<PackageComponentInput>(body.Replace(",\"consumable\":true", string.Empty))!.ToComponent("COP", 2);

        // Assert
        Assert.Equal((true, true, false, false, (bool?)true, (bool?)null), (read.IsConsumable, read.Components[0].Consumable, plain.IsConsumable, plain.Components[0].Consumable, written.Consumable, legacy.Consumable));
    }

    /// <summary>
    /// La línea de la suscripción dice si fue una suma, para que la pantalla no lea su importe como el precio del paquete.
    /// </summary>
    [Fact]
    public void From_AnAdditionLine_TellsTheScreenItsAmountIsOnlyWhatWasAdded()
    {
        // Arrange
        var (_, purchase, addition, _) = Extended();

        // Act
        var dto = PackageLineDto.From(addition.Packages![0], addition.Id, true);
        var first = PackageLineDto.From(purchase.Packages![0], purchase.Id, false);

        // Assert
        Assert.Equal((true, 600 * 11_000L, false), (dto.IsAddition, dto.SubTotal, first.IsAddition));
    }

    /// <summary>
    /// A tenant with an annual license that bought 50 a month a hundred days ago and added up to 100 twenty days ago.
    /// </summary>
    private static (TenantPackages State, OrderAggregate Purchase, OrderAggregate Addition, PackageAggregate Package) Extended()
    {
        var (state, purchase, package) = Subscribed(Consumable(), 50);
        var addition = AdditionOrder(state, package, 100);

        addition.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, Now.Minus(Duration.FromDays(20)), licensePeriodEnd: purchase.PeriodEnd, packagePeriodEnd: purchase.Packages![0].PeriodEnd);
        addition.GetAndClearEvents();

        return (new TenantPackages(SubscriptionTimeline.From([purchase])!, PackageTerm.PaidLines([purchase, addition])), purchase, addition, package);
    }
}
