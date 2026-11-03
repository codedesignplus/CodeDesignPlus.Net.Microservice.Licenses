using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.ChangeSubscription;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Queries.GetChangeOptions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;

/// <summary>
/// Cambiar de licencia (pendings/738): solo hacia una mayor, por su precio completo, con lo no prestado como anticipo, y
/// cobrando exactamente lo que se cotizó.
/// </summary>
public class ChangeSubscriptionTest
{
    private static readonly Guid Upgrade = Guid.Parse("45000000-0000-4000-8000-000000000001");
    private static readonly Guid Same = Guid.Parse("45000000-0000-4000-8000-000000000002");

    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<ILicenseRepository> licenses = new();
    private readonly Mock<ICreditBalanceRepository> creditBalances = new();
    private readonly Mock<ISubscriptionCheckout> checkout = new();
    private readonly Mock<IUserContext> user = new();
    private readonly OrderAggregate current = TestOrders.PaidFrom(SubscriptionFixtures.Now.Minus(Duration.FromDays(10)));
    private readonly LicenseAggregate upgrade = SubscriptionFixtures.Catalog(Upgrade, 30_000_000, SubscriptionFixtures.Organization, SubscriptionFixtures.Parking);
    private readonly LicenseAggregate same = SubscriptionFixtures.Catalog(Same, 9_000_000, SubscriptionFixtures.Organization);

    public ChangeSubscriptionTest()
    {
        user.SetupGet(x => x.Tenant).Returns(TestOrders.TenantId);
        user.SetupGet(x => x.IdUser).Returns(Guid.NewGuid());
        orders.Setup(x => x.FindPaidByTenantAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([current]);
        orders.Setup(x => x.FindAwaitingSubscriptionPaymentAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        licenses.Setup(x => x.FindAsync<LicenseAggregate>(Upgrade, It.IsAny<CancellationToken>())).ReturnsAsync(upgrade);
        licenses.Setup(x => x.FindAsync<LicenseAggregate>(Same, It.IsAny<CancellationToken>())).ReturnsAsync(same);
        licenses.Setup(x => x.FindOnSaleAsync(It.IsAny<CancellationToken>())).ReturnsAsync([upgrade, same]);
        checkout.Setup(x => x.StartAsync(It.IsAny<OrderAggregate>(), It.IsAny<IReadOnlyCollection<OrderAggregate>>(), It.IsAny<CancellationToken>())).ReturnsAsync(new PaymentResponse());
    }

    private ChangeSubscriptionCommandHandler Handler() => new(orders.Object, licenses.Object, creditBalances.Object, checkout.Object, user.Object);

    /// <summary>
    /// Una licencia sin nada más que la actual no se ofrece ni se acepta: no se baja ni se cambia de lado.
    /// </summary>
    [Fact]
    public async Task Handle_ALicenseThatIsNotGreater_IsRejected()
    {
        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => Handler().Handle(new ChangeSubscriptionCommand(Guid.NewGuid(), Same, null), CancellationToken.None));

        // Assert
        Assert.Equal(Errors.LicenseIsNotAnUpgrade.GetCode(), exception.Code);
    }

    /// <summary>
    /// La orden del cambio lleva el precio completo de la licencia nueva y el crédito de lo no prestado, y cobra lo mismo
    /// que dijo la cotización de las opciones de cambio.
    /// </summary>
    [Fact]
    public async Task Handle_AGreaterLicense_ChargesExactlyWhatTheChangeOptionsQuoted()
    {
        // Arrange
        OrderAggregate? created = null;
        checkout.Setup(x => x.StartAsync(It.IsAny<OrderAggregate>(), It.IsAny<IReadOnlyCollection<OrderAggregate>>(), It.IsAny<CancellationToken>()))
            .Callback<OrderAggregate, IReadOnlyCollection<OrderAggregate>, CancellationToken>((order, _, _) => created = order)
            .ReturnsAsync(new PaymentResponse());

        var options = await new GetChangeOptionsQueryHandler(orders.Object, licenses.Object, creditBalances.Object).Handle(new GetChangeOptionsQuery(TestOrders.TenantId), CancellationToken.None);

        // Act
        await Handler().Handle(new ChangeSubscriptionCommand(Guid.NewGuid(), Upgrade, null), CancellationToken.None);

        // Assert
        var quote = Assert.Single(options);
        Assert.Equal(
            (OrderType.Change, 30_000_000L, quote.Credit, quote.Total, current.License.Id),
            (created!.Type, created.License.Total.Amount, created.ProrationCredit.Amount, created.AmountToCharge.Amount, created.PreviousLicenseId!.Value));
    }

    /// <summary>
    /// Las opciones de cambio traen solo las licencias mayores, con lo que se gana y la cuenta del prorrateo: el precio
    /// menos el crédito es lo que queda por pagar.
    /// </summary>
    [Fact]
    public async Task GetChangeOptions_ListsOnlyGreaterLicensesWithTheirQuote()
    {
        // Act
        var options = await new GetChangeOptionsQueryHandler(orders.Object, licenses.Object, creditBalances.Object).Handle(new GetChangeOptionsQuery(TestOrders.TenantId), CancellationToken.None);

        // Assert
        var option = Assert.Single(options);
        Assert.Equal(
            (Upgrade, SubscriptionFixtures.Parking, option.Price - option.Credit, true),
            (option.LicenseId, option.GainedModules.Single().Id, option.Total, option.Credit > 0));
    }
}
