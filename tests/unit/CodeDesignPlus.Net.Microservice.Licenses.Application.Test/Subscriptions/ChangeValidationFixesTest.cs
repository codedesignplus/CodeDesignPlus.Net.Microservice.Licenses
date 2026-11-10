using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.FileStorage;
using CodeDesignPlus.Net.Microservice.Emails.gRpc;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.ChangeSubscription;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Xunit;
using LicenseSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.License;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;

/// <summary>
/// Lo que corrigió la validación en producción del cambio Estándar → Élite (pendings/738, 2026-11-02): el pago con la base
/// y el IVA de lo cobrado, el correo propio del cambio, el motivo de un pago rechazado y los términos obligatorios.
/// </summary>
public class ChangeValidationFixesTest
{
    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<IPubSub> pubsub = new();
    private readonly Mock<IEmailGrpc> emailGrpc = new();
    private readonly Mock<IFileStorageGrpc> fileStorage = new();
    private readonly Mock<ICurrencyGrpc> currencyGrpc = new();

    /// <summary>
    /// El cambio pagado manda su propio correo, con el total pagado de verdad: antes llegaba «Confirmación de Compra» con
    /// el precio completo y la promesa de un comprobante en otro correo.
    /// </summary>
    [Fact]
    public async Task Handle_AChangePaid_SendsTheChangeEmailWithTheAmountActuallyPaid()
    {
        // Arrange
        var order = EliteChange();
        SendEmailDomainEvent? email = null;
        orders.Setup(x => x.FindAsync<OrderAggregate>(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        orders.Setup(x => x.ApplyPaymentStatusAsync(order, PaymentStatus.Initiated, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        pubsub.Setup(x => x.PublishAsync(It.IsAny<IDomainEvent>(), It.IsAny<CancellationToken>()))
            .Callback<IDomainEvent, CancellationToken>((@event, _) => email = @event as SendEmailDomainEvent ?? email)
            .Returns(Task.CompletedTask);
        ReceiptCanBeGenerated();

        // Act
        await PaymentHandler().Handle(new UpdateStateOrderCommand(order.Id, PaymentStatus.Succeeded), CancellationToken.None);

        // Assert
        Assert.Equal(("SubscriptionChanged", "$ 428.400,00", "$ 345.100,00"), (email!.TemplateName, email.Variables["amount_paid"], email.Variables["credit_applied"]));
    }

    /// <summary>
    /// Un pago rechazado guarda en la orden el motivo y lo que dijo el banco, que trae el resultado de ms-payments.
    /// </summary>
    [Fact]
    public async Task Handle_APaymentRejected_KeepsTheReasonOnTheOrder()
    {
        // Arrange
        var order = EliteChange();
        orders.Setup(x => x.FindAsync<OrderAggregate>(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
        orders.Setup(x => x.ApplyPaymentStatusAsync(order, PaymentStatus.Initiated, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        await PaymentHandler().Handle(new UpdateStateOrderCommand(order.Id, PaymentStatus.Failed, PaymentFailureReason.Declined, "La transacción fue rechazada (Sandbox)"), CancellationToken.None);

        // Assert
        Assert.Equal((PaymentFailureReason.Declined, "La transacción fue rechazada (Sandbox)"), (order.FailureReason, order.Error));
    }

    /// <summary>
    /// El cambio exige la versión de los términos que se aceptaron, como la compra: antes la orden quedaba sin ella.
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Validate_AChangeWithoutAcceptedTerms_IsInvalid(string? version)
    {
        // Act
        var result = new Application.Subscriptions.Commands.ChangeSubscription.Validator().Validate(new ChangeSubscriptionCommand(Guid.NewGuid(), Guid.NewGuid(), version!));

        // Assert
        Assert.Contains(result.Errors, x => x.PropertyName == nameof(ChangeSubscriptionCommand.AcceptedTermsVersion));
    }

    private UpdateStateOrderCommandHandler PaymentHandler() => new(
        orders.Object, pubsub.Object, Mock.Of<ILiveChannelGrpc>(), emailGrpc.Object, fileStorage.Object, currencyGrpc.Object,
        Mock.Of<ISubscriptionSettlement>(), CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order.Services.PurchaseEmailsFixture.NotInvoiced(orders, pubsub, currencyGrpc, fileStorage), Mock.Of<ILogger<UpdateStateOrderCommandHandler>>());

    private void ReceiptCanBeGenerated()
    {
        currencyGrpc
            .Setup(x => x.GetCurrencyAsync(null, It.IsAny<string>(), null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CodeDesignPlus.Net.ValueObjects.Financial.Currency.Create(Guid.NewGuid(), "Peso Colombiano", "COP", "$", 2, 170));
        emailGrpc
            .Setup(x => x.GeneratePdfAsync(It.IsAny<GeneratePdfRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeneratePdfResponse { Success = true, PdfContent = Google.Protobuf.ByteString.CopyFrom([1, 2, 3]) });
        fileStorage
            .Setup(x => x.UploadAsync(It.IsAny<UploadFileRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UploadFileRequest request, CancellationToken _) => new UploadFileResponse { Id = request.Id, Target = request.Target, FileName = request.FileName });
    }

    /// <summary>
    /// El cambio validado: Élite (650.000 + IVA 19 %) con todo el periodo de Estándar (290.000 + IVA 19 %) por delante.
    /// </summary>
    internal static OrderAggregate EliteChange()
    {
        var now = SubscriptionFixtures.Now;
        var credited = new List<CreditedPeriod> { CreditedPeriod.Create(Guid.NewGuid(), now, now.Plus(Duration.FromDays(30)), 30, 30, Money.FromLong(29_000_000, "COP"), Money.FromLong(29_000_000, "COP"), Money.FromLong(5_510_000, "COP"), 1900) };
        var elite = Taxed(65_000_000);
        var charge = SubscriptionCharge.Compute(elite.Total, Money.FromLong(34_510_000, "COP"), Money.Zero("COP"));

        return OrderAggregate.CreateChange(Guid.NewGuid(), Guid.NewGuid(), elite, SubscriptionFixtures.Buyer(), TestOrders.BuildTenant(), Guid.NewGuid(), credited, charge, TestOrders.BuyerId);
    }

    private static LicenseSnapshot Taxed(long subTotal)
    {
        var price = Price.Create(BillingType.Monthly, Money.FromLong(subTotal, "COP"), BillingModel.FlatRate, 0, TaxTreatment.Taxed, 1900, null);
        var license = TestOrders.BuildLicense();

        return LicenseSnapshot.Create(
            license.Id, "Élite", price.Total, price.Tax, price.SubTotal, BillingType.Monthly, license.BillingModel,
            license.Description, license.ShortDescription, license.Icon, license.TermsOfService, license.IsPopular, license.ShowInLandingPage, license.Attributes, license.Modules)
            .WithBillingTerms(price, "Licencia");
    }
}
