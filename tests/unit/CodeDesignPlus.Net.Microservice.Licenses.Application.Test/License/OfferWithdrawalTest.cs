using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Cache.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.DeleteLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License;

/// <summary>
/// Retirar una licencia a medida (pendings/464, decisión del usuario del 2026-11-04): se le avisa a la copropiedad que no
/// alcanzó a tomarla, y a la que ya la tomó o la está pagando no se le puede retirar, ni quitándola de la lista ni
/// desactivando o borrando la licencia.
/// </summary>
public class OfferWithdrawalTest
{
    // La licencia de la orden pagada de las pruebas: la copropiedad que tiene esta orden ya la tomó.
    private static readonly Guid Tailored = TestOrders.BuildLicense().Id;

    private readonly Mock<ILicenseRepository> licenses = new();
    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<IPubSub> pubSub = new();
    private readonly List<IDomainEvent> published = [];

    public OfferWithdrawalTest()
    {
        orders.Setup(x => x.FindPaidByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        orders.Setup(x => x.FindAwaitingSubscriptionPaymentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        pubSub.Setup(x => x.PublishAsync(It.IsAny<IReadOnlyList<IDomainEvent>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyList<IDomainEvent>, CancellationToken>((events, _) => published.AddRange(events))
            .Returns(Task.CompletedTask);
    }

    private LicenseAggregate Offered()
    {
        var license = LicenseAggregate.Create(
            Tailored, "Élite Plus", "Plan", "Plan", [],
            [Price.Create(BillingType.Monthly, Money.FromLong(70_000_000, "COP"), BillingModel.FlatRate, 0, TaxTreatment.Taxed, 1900, null)],
            Icon.Create("solar:fire-bold", "#EC4899"), "Terms", [], isActive: true, isPopular: false, showInLandingPage: false,
            "Suscripción al servicio en la nube Kappali (SaaS)", [new OfferedTenant(TestOrders.TenantId, "Las Acacias")], Guid.NewGuid());

        license.GetAndClearEvents();
        licenses.Setup(x => x.FindAsync<LicenseAggregate>(Tailored, It.IsAny<CancellationToken>())).ReturnsAsync(license);

        return license;
    }

    private void TheTenantTookIt() => orders.Setup(x => x.FindPaidByTenantAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([TestOrders.PaidFrom(SubscriptionFixtures.Now)]);

    private void TheTenantIsPayingIt() => orders.Setup(x => x.FindAwaitingSubscriptionPaymentAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([ChangeValidationFixesTest.EliteChange()]);

    private Task Edit(bool isActive, List<OfferedTenant> offeredTo)
    {
        var mapper = new Mock<IMapper>();
        mapper.Setup(x => x.Map<List<ModuleEntity>>(It.IsAny<object>())).Returns([]);

        var user = new Mock<IUserContext>();
        user.SetupGet(x => x.IdUser).Returns(Guid.NewGuid());

        var handler = new UpdateLicenseCommandHandler(licenses.Object, orders.Object, user.Object, pubSub.Object, mapper.Object, Mock.Of<ICacheManager>(), Mock.Of<ICurrencyGrpc>());
        var request = new UpdateLicenseCommand(Tailored, "Élite Plus", "Plan", "Plan", [], [], Icon.Create("solar:fire-bold", "#EC4899"), "Terms", [], isActive, false, false, "Suscripción al servicio en la nube Kappali (SaaS)", offeredTo);

        return handler.Handle(request, CancellationToken.None);
    }

    private Task Delete()
    {
        var user = new Mock<IUserContext>();
        user.SetupGet(x => x.IdUser).Returns(Guid.NewGuid());

        return new DeleteLicenseCommandHandler(licenses.Object, orders.Object, user.Object, pubSub.Object, Mock.Of<ICacheManager>()).Handle(new DeleteLicenseCommand(Tailored), CancellationToken.None);
    }

    /// <summary>
    /// Quitar de la lista a la copropiedad que no la tomó publica el retiro, para avisarle.
    /// </summary>
    [Fact]
    public async Task Update_RemovingATenantThatDidNotTakeIt_PublishesTheWithdrawal()
    {
        // Arrange
        Offered();

        // Act
        await Edit(isActive: true, []);

        // Assert
        var withdrawn = Assert.Single(published.OfType<LicenseOfferWithdrawnDomainEvent>());
        Assert.Equal((TestOrders.TenantId, "Las Acacias", "Élite Plus"), (withdrawn.Tenant, withdrawn.TenantName, withdrawn.LicenseName));
    }

    /// <summary>
    /// A la copropiedad que ya la tomó no se le retira, y el error la nombra.
    /// </summary>
    [Fact]
    public async Task Update_RemovingATenantThatTookIt_IsRejectedNamingIt()
    {
        // Arrange
        Offered();
        TheTenantTookIt();

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => Edit(isActive: true, []));

        // Assert
        Assert.Equal((Errors.OfferAlreadyTaken.GetCode(), "Las Acacias"), (exception.Code, exception.Error?.Arguments.Single()));
    }

    /// <summary>
    /// Con un cambio a esta licencia esperando su pago tampoco se retira: hay un cobro en curso por ella.
    /// </summary>
    [Fact]
    public async Task Update_RemovingATenantWithAChangeAwaitingPayment_IsRejected()
    {
        // Arrange
        Offered();
        TheTenantIsPayingIt();

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => Edit(isActive: true, []));

        // Assert
        Assert.Equal(Errors.OfferHasAPaymentInProgress.GetCode(), exception.Code);
    }

    /// <summary>
    /// Desactivar la licencia retira todas sus ofertas: tampoco se puede si una copropiedad ya la tomó.
    /// </summary>
    [Fact]
    public async Task Update_DeactivatingALicenseATenantTook_IsRejected()
    {
        // Arrange
        Offered();
        TheTenantTookIt();

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => Edit(isActive: false, [new OfferedTenant(TestOrders.TenantId, "Las Acacias")]));

        // Assert
        Assert.Equal(Errors.OfferAlreadyTaken.GetCode(), exception.Code);
    }

    /// <summary>
    /// Desactivar la licencia que nadie tomó publica el retiro a cada copropiedad a la que se ofrecía.
    /// </summary>
    [Fact]
    public async Task Update_DeactivatingALicenseNobodyTook_PublishesTheWithdrawal()
    {
        // Arrange
        Offered();

        // Act
        await Edit(isActive: false, [new OfferedTenant(TestOrders.TenantId, "Las Acacias")]);

        // Assert
        Assert.Equal(TestOrders.TenantId, Assert.Single(published.OfType<LicenseOfferWithdrawnDomainEvent>()).Tenant);
    }

    /// <summary>
    /// Editar sin quitar a nadie no retira nada, aunque la copropiedad ya la haya tomado.
    /// </summary>
    [Fact]
    public async Task Update_KeepingATenantThatTookIt_IsAllowedAndWithdrawsNothing()
    {
        // Arrange
        Offered();
        TheTenantTookIt();

        // Act
        await Edit(isActive: true, [new OfferedTenant(TestOrders.TenantId, "Las Acacias")]);

        // Assert
        Assert.Empty(published.OfType<LicenseOfferWithdrawnDomainEvent>());
    }

    /// <summary>
    /// Borrar la licencia que una copropiedad tomó se rechaza: borrada, ya no se le podría renovar.
    /// </summary>
    [Fact]
    public async Task Delete_ALicenseATenantTook_IsRejected()
    {
        // Arrange
        Offered();
        TheTenantTookIt();

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(Delete);

        // Assert
        Assert.Equal(Errors.OfferAlreadyTaken.GetCode(), exception.Code);
    }

    /// <summary>
    /// Borrar la licencia que nadie tomó publica el retiro.
    /// </summary>
    [Fact]
    public async Task Delete_ALicenseNobodyTook_PublishesTheWithdrawal()
    {
        // Arrange
        Offered();

        // Act
        await Delete();

        // Assert
        Assert.Equal(TestOrders.TenantId, Assert.Single(published.OfType<LicenseOfferWithdrawnDomainEvent>()).Tenant);
    }
}
