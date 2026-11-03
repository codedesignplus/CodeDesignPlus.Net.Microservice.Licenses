using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Setup;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using MediatR;
using Microsoft.Extensions.Logging;
using Xunit;
using GrpcPayment = CodeDesignPlus.Net.gRpc.Clients.Services.Payment;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;

/// <summary>
/// El inicio del pago de una renovación o un cambio de licencia (pendings/735, 738).
/// </summary>
public class SubscriptionCheckoutTest
{
    private const string ReturnUrl = "https://www.kappali.test/apps/settings/organization/payment-return";

    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<ICreditBalanceRepository> creditBalances = new();
    private readonly Mock<IPaymentGrpc> payments = new();
    private readonly Mock<IMediator> mediator = new();
    private readonly SubscriptionCheckout checkout;

    public SubscriptionCheckoutTest()
    {
        MapsterConfigLicense.Configure();

        orders.Setup(x => x.CreateIfAbsentAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        orders.Setup(x => x.SupersedeAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        payments
            .Setup(x => x.InitiatePaymentAsync(It.IsAny<GrpcPayment.InitiatePaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrpcPayment.InitiatePaymentResponse { PaymentId = Guid.NewGuid().ToString(), Success = true, NextAction = GrpcPayment.NextActionType.Redirect, RedirectUrl = "https://checkout.wompi.test/l/735" });

        checkout = new SubscriptionCheckout(
            orders.Object, creditBalances.Object, payments.Object, new Mapper(), mediator.Object, Mock.Of<IUserContext>(),
            Microsoft.Extensions.Options.Options.Create(new OnlinePaymentOptions { ReturnUrl = "https://www.kappali.test/purchase/payment-return", SubscriptionReturnUrl = ReturnUrl }),
            Mock.Of<ILogger<SubscriptionCheckout>>());
    }

    /// <summary>
    /// Se cobra solo lo que queda después del saldo a favor, en la cuenta de la plataforma, volviendo a la página de retorno
    /// de Configuración › Organización; y la base más los impuestos suman lo cobrado, como exige ms-payments.
    /// </summary>
    [Fact]
    public async Task StartAsync_ARenewalWithCreditBalance_ChargesTheDifferenceInThePlatformAccount()
    {
        // Arrange
        var balance = CreditBalanceAggregate.Create(TestOrders.TenantId, "COP");
        balance.Apply(CreditMovement.ForOrder(Guid.NewGuid(), CreditMovementKind.CreditNote, Money.FromLong(4_280_000, "COP"), SubscriptionFixtures.Now));
        creditBalances.AppliesTo(balance);

        var order = SubscriptionFixtures.AwaitingRenewal(creditBalanceApplied: 4_280_000);
        GrpcPayment.InitiatePaymentRequest? sent = null;
        payments.Setup(x => x.InitiatePaymentAsync(It.IsAny<GrpcPayment.InitiatePaymentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GrpcPayment.InitiatePaymentRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(new GrpcPayment.InitiatePaymentResponse { PaymentId = Guid.NewGuid().ToString(), Success = true, NextAction = GrpcPayment.NextActionType.Redirect });

        // Act
        var response = await checkout.StartAsync(order, [], CancellationToken.None);

        // Assert
        Assert.Equal((true, ReturnUrl, 10_000_000L, 10_000_000L, 0L, order.Id), (sent!.PlatformAccount, sent.ReturnUrl, sent.Total.Value, sent.SubTotal.Value + sent.Tax.Value, balance.Balance.Amount, response.Id));
    }

    /// <summary>
    /// Élite (650.000 + IVA 123.500) cambiando desde Estándar con los 30 días por delante (290.000 + IVA 55.100): ms-payments
    /// recibe 428.400 con base 360.000 e IVA 68.400, no el neto partido como si llevara el IVA del precio dentro
    /// (pendings/738, validación del 2026-11-02).
    /// </summary>
    [Fact]
    public async Task StartAsync_AChangeWithTaxedCredit_SendsTheBaseAndTaxOfWhatIsCharged()
    {
        // Arrange
        GrpcPayment.InitiatePaymentRequest? sent = null;
        payments.Setup(x => x.InitiatePaymentAsync(It.IsAny<GrpcPayment.InitiatePaymentRequest>(), It.IsAny<CancellationToken>()))
            .Callback<GrpcPayment.InitiatePaymentRequest, CancellationToken>((request, _) => sent = request)
            .ReturnsAsync(new GrpcPayment.InitiatePaymentResponse { PaymentId = Guid.NewGuid().ToString(), Success = true, NextAction = GrpcPayment.NextActionType.Redirect });

        // Act
        await checkout.StartAsync(ChangeValidationFixesTest.EliteChange(), [], CancellationToken.None);

        // Assert
        Assert.Equal((42_840_000L, 36_000_000L, 6_840_000L), (sent!.Total.Value, sent.SubTotal.Value, sent.Tax.Value));
    }

    /// <summary>
    /// Si el saldo a favor paga todo, no hay pasarela: la orden se confirma por el mismo camino que un pago aprobado.
    /// </summary>
    [Fact]
    public async Task StartAsync_NothingLeftToCharge_ConfirmsWithoutThePaymentProvider()
    {
        // Arrange
        var balance = CreditBalanceAggregate.Create(TestOrders.TenantId, "COP");
        balance.Apply(CreditMovement.ForOrder(Guid.NewGuid(), CreditMovementKind.CreditNote, Money.FromLong(20_000_000, "COP"), SubscriptionFixtures.Now));
        creditBalances.AppliesTo(balance);

        var order = SubscriptionFixtures.AwaitingRenewal(creditBalanceApplied: 20_000_000);

        // Act
        var response = await checkout.StartAsync(order, [], CancellationToken.None);

        // Assert
        Assert.Equal((true, true), (response.Success, response.ConfirmedWithoutPayment));
        mediator.Verify(x => x.Send(It.Is<UpdateStateOrderCommand>(c => c.ReferenceId == order.Id && c.PaymentStatus == PaymentStatus.Succeeded), It.IsAny<CancellationToken>()), Times.Once);
        payments.VerifyNoOtherCalls();
    }

    /// <summary>
    /// Pedir otra renovación o un cambio reemplaza al que esperaba su pago, y el saldo que tenía apartado vuelve a estar
    /// libre antes de que la nueva lo aparte: el saldo no se gasta dos veces y nadie queda bloqueado por un pago abandonado.
    /// </summary>
    [Fact]
    public async Task StartAsync_WithAnOrderAwaitingItsPayment_SupersedesItAndReleasesItsReservedBalance()
    {
        // Arrange
        var balance = CreditBalanceAggregate.Create(TestOrders.TenantId, "COP");
        creditBalances.AppliesTo(balance);

        var abandoned = SubscriptionFixtures.AwaitingRenewal(creditBalanceApplied: 1_000_000);
        var order = SubscriptionFixtures.AwaitingRenewal(creditBalanceApplied: 1_000_000);

        // Act
        await checkout.StartAsync(order, [abandoned], CancellationToken.None);

        // Assert
        Assert.Equal((order.Id, 0L), (abandoned.SupersededBy, balance.Balance.Amount));
        orders.Verify(x => x.SupersedeAsync(abandoned, It.IsAny<CancellationToken>()), Times.Once);
    }
}
