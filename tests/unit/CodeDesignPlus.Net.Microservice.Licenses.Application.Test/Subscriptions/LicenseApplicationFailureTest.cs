using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.FileStorage;
using CodeDesignPlus.Net.Microservice.Emails.gRpc;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;

/// <summary>
/// Un cambio de licencia pagado cuya licencia no se puede aplicar a la copropiedad (pendings/738, «Élite Plus · Conjunto
/// Prueba Licencias»): el pago queda registrado y el fallo a la vista, sin tocar la licencia que la copropiedad tiene.
/// </summary>
public class LicenseApplicationFailureTest
{
    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<IPubSub> pubsub = new();
    private readonly Mock<ISubscriptionSettlement> settlement = new();
    private readonly Mock<IEmailGrpc> emailGrpc = new();

    private UpdateStateOrderCommandHandler PaymentHandler() => new(
        orders.Object, pubsub.Object, Mock.Of<ILiveChannelGrpc>(), emailGrpc.Object, Mock.Of<IFileStorageGrpc>(), Mock.Of<ICurrencyGrpc>(),
        settlement.Object, Mock.Of<ILogger<UpdateStateOrderCommandHandler>>());

    private static OrderAggregate ChangeTo(string licenseName)
    {
        var license = TestOrders.BuildLicense(licenseName);
        var credited = new List<CreditedPeriod> { new(Guid.NewGuid(), SubscriptionFixtures.Now, SubscriptionFixtures.Now.Plus(Duration.FromDays(29)), 29, 30, Money.FromLong(12_000_000, "COP"), Money.FromLong(11_600_000, "COP")) };
        var charge = SubscriptionCharge.Compute(license.Total, Money.FromLong(11_600_000, "COP"), Money.Zero("COP"));

        return OrderAggregate.CreateChange(Guid.NewGuid(), Guid.NewGuid(), license, SubscriptionFixtures.Buyer(), TestOrders.BuildTenant(), Guid.NewGuid(), credited, charge, TestOrders.BuyerId);
    }

    private OrderAggregate Arrange(string licenseName)
    {
        var order = ChangeTo(licenseName);

        orders.Setup(x => x.FindAsync<OrderAggregate>(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        orders.Setup(x => x.ApplyPaymentStatusAsync(order, PaymentStatus.Initiated, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        return order;
    }

    /// <summary>
    /// El nombre de la licencia validada en producción ya se aplica: la orden publica el cambio y lo liquida.
    /// </summary>
    [Fact]
    public async Task Handle_AChangeToALicenseNamedWithAMiddleDot_PublishesTheChange()
    {
        // Arrange
        var order = Arrange("Élite Plus · Conjunto Prueba Licencias");

        // Act
        await PaymentHandler().Handle(new UpdateStateOrderCommand(order.Id, PaymentStatus.Succeeded), CancellationToken.None);

        // Assert
        pubsub.Verify(x => x.PublishAsync(It.Is<IReadOnlyList<IDomainEvent>>(events => events.Count == 1 && events[0] is SubscriptionChangedDomainEvent), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Un cambio pagado cuya licencia no se puede aplicar guarda el pago aprobado con el fallo y publica el aviso: antes la
    /// excepción salía sin escribir nada y cada reentrega fallaba igual.
    /// </summary>
    [Fact]
    public async Task Handle_AChangePaidWhoseLicenseCannotBeApplied_StoresThePaymentAndTheFailure()
    {
        // Arrange
        var order = Arrange("Élite ★");

        // Act
        await PaymentHandler().Handle(new UpdateStateOrderCommand(order.Id, PaymentStatus.Succeeded), CancellationToken.None);

        // Assert
        orders.Verify(x => x.ApplyPaymentStatusAsync(It.Is<OrderAggregate>(o => o.PaymentStatus == PaymentStatus.Succeeded && o.ProvisioningStatus == ProvisioningStatus.PartiallyFailed), PaymentStatus.Initiated, It.IsAny<CancellationToken>()), Times.Once);
        pubsub.Verify(x => x.PublishAsync(It.Is<IReadOnlyList<IDomainEvent>>(events => events.Count == 1 && events[0] is OrderProvisioningFailedDomainEvent), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// Sin licencia aplicada no se inactiva la anterior ni se mueve el saldo, y no sale comprobante ni correo de un cambio
    /// que no ocurrió; tampoco cuando el pago se reentrega.
    /// </summary>
    [Fact]
    public async Task Handle_AChangePaidWhoseLicenseCannotBeApplied_NeitherSettlesNorSendsTheReceipt()
    {
        // Arrange
        var order = Arrange("Élite ★");

        // Act
        await PaymentHandler().Handle(new UpdateStateOrderCommand(order.Id, PaymentStatus.Succeeded), CancellationToken.None);
        await PaymentHandler().Handle(new UpdateStateOrderCommand(order.Id, PaymentStatus.Succeeded), CancellationToken.None);

        // Assert
        settlement.Verify(x => x.SettleAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        emailGrpc.Verify(x => x.GeneratePdfAsync(It.IsAny<GeneratePdfRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
