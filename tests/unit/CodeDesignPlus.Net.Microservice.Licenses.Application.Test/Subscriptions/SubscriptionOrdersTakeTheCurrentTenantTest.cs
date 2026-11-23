using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.ChangeSubscription;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.PurchasePackage;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.RenewSubscription;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Packages;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.ValueObjects.User;
using Xunit;
using TenantContracts = CodeDesignPlus.Net.gRpc.Clients.Services.Tenant;
using TenantSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.Tenant;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;

/// <summary>
/// A renewal, a license change and a package purchase are created with the tenant ms-tenants has today, not with the copy
/// of the previous order (Kappali pendings/744): the invoice, the receipt and the emails are written from the order.
/// </summary>
/// <remarks>
/// The previous order of these tests was bought as «Conjunto Prueba», 900123456 and admin@example.com; since then the
/// copropiedad corrected its name, its document and its email in ms-tenants.
/// </remarks>
public class SubscriptionOrdersTakeTheCurrentTenantTest
{
    public const string Renewal = "renewal";
    public const string Change = "change";
    public const string Package = "package";

    private static readonly Guid Upgrade = Guid.Parse("45000000-0000-4000-8000-000000000001");
    private static readonly TypeDocument TaxId = TypeDocument.Create("31", "NIT");

    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<ILicenseRepository> licenses = new();
    private readonly Mock<IPackageRepository> packages = new();
    private readonly Mock<ICreditBalanceRepository> creditBalances = new();
    private readonly Mock<ISubscriptionCheckout> checkout = new();
    private readonly Mock<IUserContext> user = new();
    private readonly Mock<ITenantGrpc> grpc = new();
    private readonly OrderAggregate previous = TestOrders.PaidFrom(SubscriptionFixtures.Now.Minus(Duration.FromDays(10)));
    private readonly List<(OrderAggregate Order, IReadOnlyCollection<OrderAggregate> Awaiting)> started = [];

    public SubscriptionOrdersTakeTheCurrentTenantTest()
    {
        user.SetupGet(x => x.Tenant).Returns(TestOrders.TenantId);
        user.SetupGet(x => x.IdUser).Returns(Guid.NewGuid());

        orders.Setup(x => x.FindPaidByTenantAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([previous]);
        orders.Setup(x => x.FindPaidWithPackagesByTenantAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        orders.Setup(x => x.FindAwaitingSubscriptionPaymentAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        orders.Setup(x => x.FindAwaitingPackagePaymentAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([]);

        licenses.Setup(x => x.FindAsync<LicenseAggregate>(previous.License.Id, It.IsAny<CancellationToken>())).ReturnsAsync(SubscriptionFixtures.Catalog(previous.License.Id, 13_000_000, SubscriptionFixtures.Organization));
        licenses.Setup(x => x.FindAsync<LicenseAggregate>(Upgrade, It.IsAny<CancellationToken>())).ReturnsAsync(SubscriptionFixtures.Catalog(Upgrade, 30_000_000, SubscriptionFixtures.Organization, SubscriptionFixtures.Parking));
        packages.Setup(x => x.FindAsync<PackageAggregate>(PackageFixtures.BasicId, It.IsAny<CancellationToken>())).ReturnsAsync(PackageFixtures.Basic());

        checkout
            .Setup(x => x.StartAsync(It.IsAny<OrderAggregate>(), It.IsAny<IReadOnlyCollection<OrderAggregate>>(), It.IsAny<CancellationToken>()))
            .Callback<OrderAggregate, IReadOnlyCollection<OrderAggregate>, CancellationToken>((order, awaiting, _) => started.Add((order, awaiting)))
            .ReturnsAsync(new PaymentResponse());

        grpc.Has(SubscriptionFixtures.AnswerOf(Current()));
    }

    private static TenantSnapshot Current(string numberDocument = "901777333-0") => TenantSnapshot.Create(
        TestOrders.TenantId, "Conjunto Las Acacias", null, TaxId, numberDocument, "+573009876543", "administracion@acacias.example.com", TestOrders.BuildTenant().Location);

    private ITenantSnapshots Tenants() => new TenantSnapshots(grpc.Object);

    private Task<PaymentResponse> SendAsync(string kind, Guid? id = null) => kind switch
    {
        Renewal => new RenewSubscriptionCommandHandler(orders.Object, licenses.Object, creditBalances.Object, checkout.Object, user.Object, packages.Object, Tenants()).Handle(new RenewSubscriptionCommand(id ?? Guid.NewGuid(), null), CancellationToken.None),
        Change => new ChangeSubscriptionCommandHandler(orders.Object, licenses.Object, creditBalances.Object, checkout.Object, user.Object, packages.Object, Tenants()).Handle(new ChangeSubscriptionCommand(id ?? Guid.NewGuid(), Upgrade, null), CancellationToken.None),
        _ => new PurchasePackageCommandHandler(orders.Object, packages.Object, creditBalances.Object, checkout.Object, user.Object, Tenants()).Handle(new PurchasePackageCommand(id ?? Guid.NewGuid(), PackageFixtures.BasicId, null, "2026-11-02"), CancellationToken.None)
    };

    private OrderAggregate Awaiting(string kind, TenantSnapshot tenant)
    {
        var license = kind == Change ? SubscriptionSnapshots.Of(SubscriptionFixtures.Catalog(Upgrade, 30_000_000, SubscriptionFixtures.Organization, SubscriptionFixtures.Parking), SubscriptionFixtures.Catalog(Upgrade, 30_000_000).Prices[0]) : previous.License;
        var charge = SubscriptionCharge.Compute(license.Total, Money.Zero("COP"), Money.Zero("COP"));

        var pending = kind == Change
            ? OrderAggregate.CreateChange(Guid.NewGuid(), Guid.NewGuid(), license, SubscriptionFixtures.Buyer(), tenant, previous.License.Id, [], charge, TestOrders.BuyerId)
            : OrderAggregate.CreateRenewal(Guid.NewGuid(), Guid.NewGuid(), license, SubscriptionFixtures.Buyer(), tenant, charge, TestOrders.BuyerId);

        orders.Setup(x => x.FindAwaitingSubscriptionPaymentAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([pending]);

        return pending;
    }

    [Theory]
    [InlineData(Renewal, OrderType.Renewal)]
    [InlineData(Change, OrderType.Change)]
    [InlineData(Package, OrderType.Package)]
    public async Task Handle_TenantCorrectedSinceThePreviousOrder_CreatesTheOrderWithWhatMsTenantsHasToday(string kind, OrderType type)
    {
        // Act
        await SendAsync(kind);

        // Assert
        var order = Assert.Single(started).Order;

        Assert.Equal(
            (type, "901777333-0", "31", "Conjunto Las Acacias", "administracion@acacias.example.com", "+573009876543"),
            (order.Type, order.TenantDetail.NumberDocument, order.TenantDetail.TypeDocument.Code, order.TenantDetail.Name, order.TenantDetail.Email, order.TenantDetail.Phone));
    }

    /// <summary>
    /// The order is of the copropiedad that renews: ms-tenants is asked for the tenant of the request, once, and for no other.
    /// </summary>
    [Theory]
    [InlineData(Renewal)]
    [InlineData(Change)]
    [InlineData(Package)]
    public async Task Handle_AnyOrder_AsksMsTenantsForTheTenantOfTheRequest(string kind)
    {
        // Act
        await SendAsync(kind);

        // Assert
        grpc.Verify(x => x.GetTenantByIdAsync(It.Is<TenantContracts.GetTenantRequest>(r => r.Id == TestOrders.TenantId.ToString()), It.IsAny<CancellationToken>()), Times.Once);
        grpc.Verify(x => x.GetTenantByIdAsync(It.Is<TenantContracts.GetTenantRequest>(r => r.Id != TestOrders.TenantId.ToString()), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The user decided a copropiedad is never left without renewing for its document: with a tax id without its check digit
    /// the order is created and charged all the same. What waits is its invoice.
    /// </summary>
    [Theory]
    [InlineData(Renewal, "901777333")]
    [InlineData(Change, "901777333-5")]
    [InlineData(Package, "9017773330")]
    public async Task Handle_TenantDocumentWithoutItsCheckDigit_CreatesTheOrderAllTheSame(string kind, string numberDocument)
    {
        // Arrange
        grpc.Has(SubscriptionFixtures.AnswerOf(Current(numberDocument)));

        // Act
        await SendAsync(kind);

        // Assert
        Assert.Equal(numberDocument, Assert.Single(started).Order.TenantDetail.NumberDocument);
    }

    /// <summary>
    /// An order waiting for its payment with the data of before is not reused: a new one is created and replaces it, so
    /// what is paid is invoiced with the current data.
    /// </summary>
    [Theory]
    [InlineData(Renewal)]
    [InlineData(Change)]
    public async Task Handle_OrderAwaitingItsPaymentWithTheDataOfBefore_IsReplacedByANewOne(string kind)
    {
        // Arrange
        var pending = Awaiting(kind, TestOrders.BuildTenant());

        // Act
        await SendAsync(kind);

        // Assert
        var (order, awaiting) = Assert.Single(started);

        Assert.Equal((true, "901777333-0", pending.Id), (order.Id != pending.Id, order.TenantDetail.NumberDocument, awaiting.Single().Id));
    }

    /// <summary>
    /// With the same data, asking again returns the payment of the one already waiting: two are not created.
    /// </summary>
    [Theory]
    [InlineData(Renewal)]
    [InlineData(Change)]
    public async Task Handle_OrderAwaitingItsPaymentWithTheCurrentData_ReturnsItsPayment(string kind)
    {
        // Arrange
        var pending = Awaiting(kind, Current());

        // Act
        var response = await SendAsync(kind);

        // Assert
        Assert.Equal((pending.Id, 0), (response.Id, started.Count));
    }

    /// <summary>
    /// The retry of the same form answers with the order already saved, without asking ms-tenants again.
    /// </summary>
    [Theory]
    [InlineData(Renewal)]
    [InlineData(Change)]
    [InlineData(Package)]
    public async Task Handle_RetryOfTheSameForm_DoesNotAskMsTenants(string kind)
    {
        // Arrange
        var stored = SubscriptionFixtures.AwaitingRenewal();
        orders.Setup(x => x.FindAsync<OrderAggregate>(stored.Id, It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        // Act
        await SendAsync(kind, stored.Id);

        // Assert
        grpc.Verify(x => x.GetTenantByIdAsync(It.IsAny<TenantContracts.GetTenantRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The phone ms-tenants has does not fit in an order: nothing is created nor charged, and the error says which data of
    /// the copropiedad to correct and where (rule 44).
    /// </summary>
    [Theory]
    [InlineData(Renewal)]
    [InlineData(Change)]
    [InlineData(Package)]
    public async Task Handle_TenantPhoneTheOrderDoesNotAccept_ChargesNothingAndSaysWhatToCorrect(string kind)
    {
        // Arrange
        var answer = SubscriptionFixtures.AnswerOf(Current());
        answer.Phone = "300 987 6543";
        grpc.Has(answer);

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => SendAsync(kind));

        // Assert
        Assert.Equal(
            (Errors.TenantPhoneIsNotValid.GetCode(), "El teléfono de tu copropiedad, '300 987 6543', no es válido: va solo con dígitos, entre 7 y 15, y puede empezar por +. Corrígelo en Organización y vuelve a intentarlo.", 0),
            (exception.Code, exception.Error!.Value.GetMessage("es"), started.Count));
    }

    [Theory]
    [InlineData(Renewal)]
    [InlineData(Change)]
    [InlineData(Package)]
    public async Task Handle_TenantEmailTheOrderDoesNotAccept_ChargesNothingAndSaysWhatToCorrect(string kind)
    {
        // Arrange
        var answer = SubscriptionFixtures.AnswerOf(Current());
        answer.Email = "administracion-acacias";
        grpc.Has(answer);

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => SendAsync(kind));

        // Assert
        Assert.Equal(
            (Errors.TenantEmailIsNotValid.GetCode(), "El correo de tu copropiedad, 'administracion-acacias', no es válido. Corrígelo en Organización y vuelve a intentarlo.", 0),
            (exception.Code, exception.Error!.Value.GetMessage("es"), started.Count));
    }
}
