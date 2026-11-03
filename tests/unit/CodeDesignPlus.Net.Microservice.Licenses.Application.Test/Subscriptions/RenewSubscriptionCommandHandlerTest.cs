using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.RenewSubscription;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;

/// <summary>
/// Renovar la licencia (pendings/735): la licencia vigente con el precio vigente del catálogo y el saldo a favor aplicado.
/// </summary>
public class RenewSubscriptionCommandHandlerTest
{
    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<ILicenseRepository> licenses = new();
    private readonly Mock<ICreditBalanceRepository> creditBalances = new();
    private readonly Mock<ISubscriptionCheckout> checkout = new();
    private readonly Mock<IUserContext> user = new();
    private readonly RenewSubscriptionCommandHandler handler;
    private readonly OrderAggregate current = TestOrders.PaidFrom(SubscriptionFixtures.Now.Minus(Duration.FromDays(10)));

    public RenewSubscriptionCommandHandlerTest()
    {
        user.SetupGet(x => x.Tenant).Returns(TestOrders.TenantId);
        user.SetupGet(x => x.IdUser).Returns(Guid.NewGuid());
        orders.Setup(x => x.FindPaidByTenantAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([current]);
        orders.Setup(x => x.FindAwaitingSubscriptionPaymentAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        checkout.Setup(x => x.StartAsync(It.IsAny<OrderAggregate>(), It.IsAny<IReadOnlyCollection<OrderAggregate>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PaymentResponse());

        handler = new RenewSubscriptionCommandHandler(orders.Object, licenses.Object, creditBalances.Object, checkout.Object, user.Object, Mock.Of<IPackageRepository>());
    }

    /// <summary>
    /// La renovación copia los módulos y atributos que la copropiedad tiene por contrato, pero cobra el precio vigente del
    /// catálogo, y aplica el saldo a favor.
    /// </summary>
    [Fact]
    public async Task Handle_ARenewal_CopiesTheCurrentLicenseWithTheCurrentCatalogPriceAndTheCreditBalance()
    {
        // Arrange
        licenses.Setup(x => x.FindAsync<LicenseAggregate>(current.License.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(SubscriptionFixtures.Catalog(current.License.Id, 13_000_000, SubscriptionFixtures.Organization, SubscriptionFixtures.Parking));

        var balance = CreditBalanceAggregate.Create(TestOrders.TenantId, "COP");
        balance.Apply(CreditMovement.ForOrder(Guid.NewGuid(), CreditMovementKind.CreditNote, Money.FromLong(3_000_000, "COP"), SubscriptionFixtures.Now));
        creditBalances.Setup(x => x.FindAsync<CreditBalanceAggregate>(TestOrders.TenantId, TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync(balance);

        OrderAggregate? created = null;
        checkout.Setup(x => x.StartAsync(It.IsAny<OrderAggregate>(), It.IsAny<IReadOnlyCollection<OrderAggregate>>(), It.IsAny<CancellationToken>()))
            .Callback<OrderAggregate, IReadOnlyCollection<OrderAggregate>, CancellationToken>((order, _, _) => created = order)
            .ReturnsAsync(new PaymentResponse());

        // Act
        await handler.Handle(new RenewSubscriptionCommand(Guid.NewGuid(), null), CancellationToken.None);

        // Assert
        Assert.Equal(
            (OrderType.Renewal, 13_000_000L, 1, 3_000_000L, 10_000_000L),
            (created!.Type, created.License.Total.Amount, created.License.Modules.Count, created.CreditBalanceApplied!.Amount, created.AmountToCharge.Amount));
    }

    /// <summary>
    /// Pedirla otra vez mientras la anterior espera su pago devuelve el cobro de la anterior: no se crean dos.
    /// </summary>
    [Fact]
    public async Task Handle_ARenewalAlreadyAwaitingItsPayment_ReturnsItsPayment()
    {
        // Arrange
        var pending = SubscriptionFixtures.AwaitingRenewal();
        orders.Setup(x => x.FindAwaitingSubscriptionPaymentAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([pending]);

        // Act
        var response = await handler.Handle(new RenewSubscriptionCommand(Guid.NewGuid(), null), CancellationToken.None);

        // Assert
        Assert.Equal(pending.Id, response.Id);
        checkout.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Una licencia borrada del catálogo ya no tiene precio vigente: no se renueva, y el error invita a cambiar de licencia.
    /// </summary>
    [Fact]
    public async Task Handle_ALicenseDeletedFromTheCatalog_CannotBeRenewed()
    {
        // Arrange
        var catalog = SubscriptionFixtures.Catalog(current.License.Id, 13_000_000, SubscriptionFixtures.Organization);
        catalog.Delete(Guid.NewGuid());
        licenses.Setup(x => x.FindAsync<LicenseAggregate>(current.License.Id, It.IsAny<CancellationToken>())).ReturnsAsync(catalog);

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => handler.Handle(new RenewSubscriptionCommand(Guid.NewGuid(), null), CancellationToken.None));

        // Assert
        Assert.Equal(Errors.LicenseCannotBeRenewed.GetCode(), exception.Code);
    }

    /// <summary>
    /// Con el id de una orden de otra copropiedad no se devuelve su cobro.
    /// </summary>
    [Fact]
    public async Task Handle_TheIdOfAnotherTenantsOrder_IsRejected()
    {
        // Arrange
        var stored = SubscriptionFixtures.AwaitingRenewal();
        orders.Setup(x => x.FindAsync<OrderAggregate>(stored.Id, It.IsAny<CancellationToken>())).ReturnsAsync(stored);
        user.SetupGet(x => x.Tenant).Returns(Guid.NewGuid());

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => handler.Handle(new RenewSubscriptionCommand(stored.Id, null), CancellationToken.None));

        // Assert
        Assert.Equal(Errors.SubscriptionOrderBelongsToAnotherTenant.GetCode(), exception.Code);
    }
}
