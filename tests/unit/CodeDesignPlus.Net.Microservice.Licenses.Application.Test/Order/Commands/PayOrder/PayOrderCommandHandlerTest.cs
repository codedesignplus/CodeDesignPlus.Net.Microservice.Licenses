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
    private readonly PayOrderCommandHandler handler;

    public PayOrderCommandHandlerTest()
    {
        handler = new PayOrderCommandHandler(
            repositoryMock.Object,
            Mock.Of<IUserContext>(),
            Mock.Of<IPubSub>(),
            Mock.Of<IMapper>(),
            Mock.Of<IPaymentGrpc>(),
            tenantGrpcMock.Object);
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
}
