using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Cache.Abstractions;
using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetAllLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetLicenseById;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Setup;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Queries.GetChangeOptions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Xunit;
using GrpcPayment = CodeDesignPlus.Net.gRpc.Clients.Services.Payment;
using OrderLicenseDto = CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects.LicenseDto;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License;

/// <summary>
/// Las licencias a medida en el catálogo (pendings/464) y el tratamiento tributario que llega a la orden (pendings/662):
/// la licencia privada no sale en el catálogo público ni se compra, se le ofrece solo a la copropiedad elegida y nunca le
/// baja la licencia.
/// </summary>
public class CustomLicenseCatalogTest
{
    private static readonly Guid Elite = Guid.Parse("46400000-0000-4000-8000-000000000101");
    private static readonly Guid OtherTenant = Guid.Parse("46400000-0000-4000-8000-000000000102");

    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<ILicenseRepository> licenses = new();
    private readonly Mock<ICreditBalanceRepository> creditBalances = new();

    private static LicenseAggregate Private(Guid id, long subTotal, List<OfferedTenant> offeredTo, params Guid[] modules) => LicenseAggregate.Create(
        id, "Élite a medida", "Plan", "Plan",
        [.. modules.Select(x => new ModuleEntity { Id = x, Name = x == SubscriptionFixtures.Parking ? "Parqueaderos" : "Organización", Description = "Módulo" })],
        [Price.Create(BillingType.Monthly, Money.FromLong(subTotal, "COP"), BillingModel.FlatRate, 0, TaxTreatment.Excluded, 0, "Art. 476 num. 21 ET")],
        Icon.Create("solar:fire-bold", "#EC4899"), "Terms", [], isActive: true, isPopular: false, showInLandingPage: false, "Suscripción al servicio en la nube Kappali (SaaS)", offeredTo, Guid.NewGuid());

    private static List<OfferedTenant> ToTheTenant() => [new OfferedTenant(TestOrders.TenantId, "Las Acacias")];

    /// <summary>
    /// Una licencia a medida no se compra desde la landing: se le ofrece a una copropiedad que ya existe y la paga con el
    /// cambio de licencia.
    /// </summary>
    [Fact]
    public async Task PayOrder_APrivateLicense_IsRejected()
    {
        // Arrange
        var license = Private(Elite, 30_000_000, ToTheTenant(), SubscriptionFixtures.Organization);
        licenses.Setup(x => x.FindAsync<LicenseAggregate>(Elite, It.IsAny<CancellationToken>())).ReturnsAsync(license);
        var request = new PayOrderCommand(Guid.NewGuid(), null!, new OrderLicenseDto { Id = Elite, BillingType = BillingType.Monthly }, TestOrders.BuildTenant(), "2026-11-02");

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => PayOrder(new Mock<IPaymentGrpc>()).Handle(request, CancellationToken.None));

        // Assert
        Assert.Equal(Errors.LicenseIsNotForSale.GetCode(), exception.Code);
    }

    /// <summary>
    /// La orden de compra guarda la versión de los términos aceptada y el tratamiento tributario y el concepto del precio.
    /// </summary>
    [Fact]
    public async Task PayOrder_APublicLicense_KeepsTheAcceptedTermsAndTheTaxTreatmentInTheOrder()
    {
        // Arrange
        var license = LicenseAggregate.Create(
            Elite, "Esencial", "Plan", "Plan", [],
            [Price.Create(BillingType.Monthly, Money.FromLong(12_000_000, "COP"), BillingModel.FlatRate, 0, TaxTreatment.Excluded, 0, "Art. 476 num. 21 ET")],
            Icon.Create("solar:fire-bold", "#EC4899"), "Terms", [], isActive: true, isPopular: false, showInLandingPage: true, "Suscripción al servicio en la nube Kappali (SaaS)", [], Guid.NewGuid());
        var payments = new Mock<IPaymentGrpc>();
        OrderAggregate? created = null;

        licenses.Setup(x => x.FindAsync<LicenseAggregate>(Elite, It.IsAny<CancellationToken>())).ReturnsAsync(license);
        // La orden se lee al guardarla; el guardado responde como el de un envío repetido para que la prueba no pase por la
        // pasarela ni por Mapster, cuya configuración global la cambian en paralelo otras clases de prueba.
        orders.Setup(x => x.CreateIfAbsentAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>())).Callback<OrderAggregate, CancellationToken>((order, _) => created = order).ReturnsAsync(false);
        payments.Setup(x => x.InitiatePaymentAsync(It.IsAny<GrpcPayment.InitiatePaymentRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(new GrpcPayment.InitiatePaymentResponse { PaymentId = Guid.NewGuid().ToString(), Success = true });

        var request = new PayOrderCommand(Guid.NewGuid(), Buyer(), new OrderLicenseDto { Id = Elite, BillingType = BillingType.Monthly }, TestOrders.BuildTenant(), "2026-11-02");

        // Act
        await PayOrder(payments).Handle(request, CancellationToken.None);

        // Assert
        Assert.Equal(("2026-11-02", TaxTreatment.Excluded, "Art. 476 num. 21 ET", "Suscripción al servicio en la nube Kappali (SaaS)"), (created!.AcceptedTermsVersion, created.License.TaxTreatment, created.License.TaxLegalBasis, created.License.InvoiceConcept));
    }

    /// <summary>
    /// El catálogo público pide al repositorio solo las licencias de la landing; la vista de plataforma las pide todas.
    /// </summary>
    [Theory]
    [InlineData(true, 1, 0)]
    [InlineData(false, 0, 1)]
    public async Task GetAllLicense_ForSaleOnly_ReadsOnlyTheLicensesOfTheLandingPage(bool forSaleOnly, int publicReads, int allReads)
    {
        // Arrange
        var empty = Pagination<LicenseAggregate>.Create([], 0, 10, 0);
        licenses.Setup(x => x.MatchingShownInLandingPageAsync(It.IsAny<C.Criteria>(), It.IsAny<CancellationToken>())).ReturnsAsync(empty);
        licenses.Setup(x => x.MatchingAsync<LicenseAggregate>(It.IsAny<C.Criteria>(), It.IsAny<CancellationToken>())).ReturnsAsync(empty);

        // Act
        await new GetAllLicenseQueryHandler(licenses.Object, Mock.Of<IMapper>()).Handle(new GetAllLicenseQuery(new C.Criteria(), forSaleOnly), CancellationToken.None);

        // Assert
        licenses.Verify(x => x.MatchingShownInLandingPageAsync(It.IsAny<C.Criteria>(), It.IsAny<CancellationToken>()), Times.Exactly(publicReads));
        licenses.Verify(x => x.MatchingAsync<LicenseAggregate>(It.IsAny<C.Criteria>(), It.IsAny<CancellationToken>()), Times.Exactly(allReads));
    }

    /// <summary>
    /// El detalle público de una licencia privada responde igual que una que no existe, aunque esté en la caché.
    /// </summary>
    [Fact]
    public async Task GetLicenseById_APrivateLicenseFromTheCatalog_IsNotFound()
    {
        // Arrange
        var cache = new Mock<ICacheManager>();
        cache.Setup(x => x.ExistsAsync(Elite.ToString())).ReturnsAsync(true);
        cache.Setup(x => x.GetAsync<LicenseDto>(Elite.ToString())).ReturnsAsync(new LicenseDto { Id = Elite, ShowInLandingPage = false });

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => new GetLicenseByIdQueryHandler(licenses.Object, Mock.Of<IMapper>(), cache.Object).Handle(new GetLicenseByIdQuery(Elite, ForSaleOnly: true), CancellationToken.None));

        // Assert
        Assert.Equal(Errors.LicenseNotFound.GetCode(), exception.Code);
    }

    /// <summary>
    /// La licencia a medida aparece entre las opciones de cambio de la copropiedad a la que se ofreció, con su tratamiento
    /// tributario.
    /// </summary>
    [Fact]
    public async Task GetChangeOptions_APrivateLicenseOfferedToTheTenant_IsAnOption()
    {
        // Arrange
        var custom = Private(Elite, 20_000_000, ToTheTenant(), SubscriptionFixtures.Organization, SubscriptionFixtures.Parking);
        SubscribedWith(custom);

        // Act
        var options = await new GetChangeOptionsQueryHandler(orders.Object, licenses.Object, creditBalances.Object, Mock.Of<IPackageRepository>()).Handle(new GetChangeOptionsQuery(TestOrders.TenantId), CancellationToken.None);

        // Assert
        var option = Assert.Single(options);
        Assert.Equal((Elite, true, TaxTreatment.Excluded, 0), (option.LicenseId, option.IsTailored, option.TaxTreatment, option.TaxBasisPoints));
    }

    /// <summary>
    /// A otra copropiedad no le aparece.
    /// </summary>
    [Fact]
    public async Task GetChangeOptions_APrivateLicenseOfferedToAnotherTenant_IsNotAnOption()
    {
        // Arrange
        var custom = Private(Elite, 20_000_000, [new OfferedTenant(OtherTenant, "Los Cerezos")], SubscriptionFixtures.Organization, SubscriptionFixtures.Parking);
        SubscribedWith(custom);

        // Act
        var options = await new GetChangeOptionsQueryHandler(orders.Object, licenses.Object, creditBalances.Object, Mock.Of<IPackageRepository>()).Handle(new GetChangeOptionsQuery(TestOrders.TenantId), CancellationToken.None);

        // Assert
        Assert.Empty(options);
    }

    /// <summary>
    /// Una licencia a medida nunca baja la licencia del cliente: ofrecer una que no suma nada se rechaza al guardarla.
    /// </summary>
    [Fact]
    public async Task EnsureEachIsAnUpgrade_ALicenseThatAddsNothing_IsRejected()
    {
        // Arrange
        var custom = Private(Elite, 5_000_000, ToTheTenant(), SubscriptionFixtures.Organization);
        SubscribedWith(custom);

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => LicenseOffers.EnsureEachIsAnUpgradeAsync(custom, orders.Object, CancellationToken.None));

        // Assert
        Assert.Equal(Errors.LicenseIsNotAnUpgradeForAnOfferedTenant.GetCode(), exception.Code);
    }

    /// <summary>
    /// Una mejora que cuesta menos que la licencia actual se acepta: el sobrante del crédito queda como saldo a favor.
    /// </summary>
    [Fact]
    public async Task EnsureEachIsAnUpgrade_ACheaperLicenseWithMoreModules_IsAccepted()
    {
        // Arrange
        var custom = Private(Elite, 1_000_000, ToTheTenant(), SubscriptionFixtures.Organization, SubscriptionFixtures.Parking);
        SubscribedWith(custom);

        // Act
        var exception = await Record.ExceptionAsync(() => LicenseOffers.EnsureEachIsAnUpgradeAsync(custom, orders.Object, CancellationToken.None));

        // Assert
        Assert.Null(exception);
    }

    /// <summary>
    /// No se le ofrece a una copropiedad sin suscripción pagada: no tendría cómo cambiarse a ella.
    /// </summary>
    [Fact]
    public async Task EnsureEachIsAnUpgrade_ATenantWithoutSubscription_IsRejected()
    {
        // Arrange
        var custom = Private(Elite, 20_000_000, ToTheTenant(), SubscriptionFixtures.Organization, SubscriptionFixtures.Parking);
        orders.Setup(x => x.FindPaidByTenantAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => LicenseOffers.EnsureEachIsAnUpgradeAsync(custom, orders.Object, CancellationToken.None));

        // Assert
        Assert.Equal(Errors.OfferedTenantHasNoSubscription.GetCode(), exception.Code);
    }

    /// <summary>
    /// El recibo ya no dice «IVA (19%)» fijo: dice el tratamiento del precio comprado.
    /// </summary>
    [Theory]
    [InlineData(TaxTreatment.Taxed, 1900, null, "IVA (19 %)")]
    [InlineData(TaxTreatment.Taxed, 1650, null, "IVA (16,5 %)")]
    [InlineData(TaxTreatment.Excluded, 0, "Art. 476 num. 21 ET", "Excluido de IVA")]
    [InlineData(TaxTreatment.Exempt, 0, "Norma", "Exento de IVA")]
    public void Receipt_TheTaxRow_FollowsTheTreatmentOfThePrice(TaxTreatment treatment, int taxBasisPoints, string? legalBasis, string expected)
    {
        // Arrange
        var price = Price.Create(BillingType.Monthly, Money.FromLong(12_000_000, "COP"), BillingModel.FlatRate, 0, treatment, taxBasisPoints, legalBasis);
        var order = TestOrders.PaidFrom(SubscriptionFixtures.Now, license: TestOrders.BuildLicense().WithBillingTerms(price, "Concepto"));

        // Act
        var variables = ReceiptEmailVariables.Build(order, TestOrders.Cop);

        // Assert
        Assert.Equal(expected, variables["tax_label"]);
    }

    private void SubscribedWith(LicenseAggregate custom)
    {
        var current = TestOrders.PaidFrom(SubscriptionFixtures.Now.Minus(Duration.FromDays(10)));

        orders.Setup(x => x.FindPaidByTenantAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([current]);
        orders.Setup(x => x.FindAwaitingSubscriptionPaymentAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        licenses.Setup(x => x.FindOnSaleAsync(It.IsAny<CancellationToken>())).ReturnsAsync([custom]);
    }

    private PayOrderCommandHandler PayOrder(Mock<IPaymentGrpc> payments)
    {
        MapsterConfigLicense.Configure();

        var user = new Mock<IUserContext>();
        user.SetupGet(x => x.IdUser).Returns(Guid.NewGuid());

        return new PayOrderCommandHandler(
            licenses.Object,
            user.Object,
            Mock.Of<IPubSub>(),
            new Mapper(),
            payments.Object,
            Mock.Of<ITenantGrpc>(),
            orders.Object,
            Microsoft.Extensions.Options.Options.Create(new OnlinePaymentOptions { ReturnUrl = "https://www.kappali.test/purchase/payment-return" }),
            Mock.Of<IPackageRepository>());
    }

    private static CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects.BuyerDto Buyer() => new()
    {
        Name = "Copropiedad Malpelo",
        Phone = "+573001234567",
        Email = "compras@malpelo.com",
        TypeDocument = CodeDesignPlus.Net.ValueObjects.User.TypeDocument.Create("NIT", "Número de Identificación Tributaria"),
        Document = "900123456",
    };
}
