using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order.Commands.PayOrder;

public class PayOrderCommandHandlerTest
{
    private readonly Mock<ILicenseRepository> repositoryMock = new();
    private readonly Mock<ITenantGrpc> tenantGrpcMock = new();
    private readonly Mock<IOrderRepository> ordersMock = new();
    private readonly Mock<IPaymentGrpc> paymentGrpcMock = new();
    private readonly Mock<IUserContext> userMock = new();
    private readonly PayOrderCommandHandler handler;

    public PayOrderCommandHandlerTest()
    {
        handler = new PayOrderCommandHandler(
            repositoryMock.Object,
            userMock.Object,
            Mock.Of<IPubSub>(),
            Mock.Of<IMapper>(),
            paymentGrpcMock.Object,
            tenantGrpcMock.Object,
            ordersMock.Object);
    }

    /// <summary>
    /// Una licencia retirada de la venta no se puede comprar aunque el comprador conserve su id: la página de precios
    /// la mostraba y el pedido se cobraba igual (plan 056 de pendings/). Se rechaza antes de tocar el tenant o la orden.
    /// </summary>
    [Fact]
    public async Task Handle_LicenseIsNotActive_RejectsThePurchaseBeforeCreatingAnything()
    {
        var license = LicenseAggregate.Create(
            Guid.NewGuid(), "Retired", "Short", "Description", [],
            [Price.Create(BillingType.Monthly, Money.FromDecimal(100, "COP", 2), BillingModel.FlatRate, 0, 1900)],
            Icon.Create("solar:fire-bold", "#EC4899"), "Terms", [], isActive: false, isPopular: false, showInLandingPage: true, Guid.NewGuid());

        var request = new PayOrderCommand(Guid.NewGuid(), null!, new LicenseDto { Id = license.Id, BillingType = BillingType.Monthly }, null!, null!);

        repositoryMock
            .Setup(r => r.FindAsync<LicenseAggregate>(license.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(license);

        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => handler.Handle(request, CancellationToken.None));

        Assert.Equal(Errors.LicenseIsNotActive.GetCode(), exception.Code);
        Assert.Equal(Layer.Application, exception.Layer);
        tenantGrpcMock.Verify(t => t.ExistTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        repositoryMock.Verify(r => r.CreateAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// La orden que llega dos veces con el mismo id es el reintento del mismo checkout (pendings/589, regla 66): el segundo
    /// envío recibe la respuesta del cobro ya iniciado, sin volver a la pasarela. Antes respondía «ya existe».
    /// </summary>
    [Fact]
    public async Task Handle_TheSameOrderAgain_ReturnsThePaymentAlreadyInitiated()
    {
        var buyerId = Guid.NewGuid();
        var order = StoredOrder(buyerId, new InitiatedPayment(true, (int)NextActionType.Redirect, "https://sandbox.payu.test/pse/589", []));
        userMock.SetupGet(u => u.IdUser).Returns(buyerId);

        var response = await handler.Handle(new PayOrderCommand(order.Id, null!, null!, null!, null!), CancellationToken.None);

        Assert.Equal((order.PaymentId, true, NextActionType.Redirect, "https://sandbox.payu.test/pse/589"), (response.PaymentId, response.Success, response.NextAction, response.RedirectUrl));
        paymentGrpcMock.VerifyNoOtherCalls();
        tenantGrpcMock.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Una orden ajena con el mismo id sigue siendo «ya existe»: el reintento es solo de quien la creó.
    /// </summary>
    [Fact]
    public async Task Handle_SomeoneElsesOrderId_IsStillRejected()
    {
        var order = StoredOrder(Guid.NewGuid(), null);
        userMock.SetupGet(u => u.IdUser).Returns(Guid.NewGuid());

        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => handler.Handle(new PayOrderCommand(order.Id, null!, null!, null!, null!), CancellationToken.None));

        Assert.Equal(Errors.OrderAlreadyExists.GetCode(), exception.Code);
        paymentGrpcMock.VerifyNoOtherCalls();
    }

    private OrderAggregate StoredOrder(Guid createdBy, InitiatedPayment? payment)
    {
        var order = (OrderAggregate)System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(typeof(OrderAggregate));

        Set(order, nameof(OrderAggregate.Id), Guid.NewGuid());
        Set(order, nameof(OrderAggregate.PaymentId), Guid.NewGuid());
        Set(order, nameof(OrderAggregate.CreatedBy), createdBy);
        Set(order, nameof(OrderAggregate.InitiatedPayment), payment);

        ordersMock.Setup(o => o.FindAsync<OrderAggregate>(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);

        return order;
    }

    private static void Set(object target, string property, object? value)
    {
        var type = target.GetType();

        while (type is not null)
        {
            var info = type.GetProperty(property, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.DeclaredOnly);

            if (info?.SetMethod is not null)
            {
                info.SetValue(target, value);
                return;
            }

            type = type.BaseType;
        }

        throw new InvalidOperationException(property);
    }
}
