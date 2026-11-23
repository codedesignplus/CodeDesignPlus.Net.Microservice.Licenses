using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Setup;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.ChangeSubscription;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.PurchasePackage;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.RenewSubscription;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.User;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;
using GrpcPayment = CodeDesignPlus.Net.gRpc.Clients.Services.Payment;
using TenantSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.Tenant;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order.Commands.PayOrder;

/// <summary>
/// La compra que crea una copropiedad no acepta su documento, si el tipo lleva dígito de verificación, sin guion o con otro
/// dígito, y lo rechaza antes de crear la orden y de iniciar el cobro (pendings/744). La renovación, el cambio de licencia
/// y la compra de paquetes toman la copropiedad de ms-tenants y no validan su documento: no se quedan sin renovar por él.
/// </summary>
public class PayOrderTenantDocumentTest
{
    private static readonly IOptions<TenantDocumentOptions> Configured = Microsoft.Extensions.Options.Options.Create(new TenantDocumentOptions
    {
        CheckDigitDocumentTypes = ["31"],
        CheckDigitWeights = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71]
    });

    private static readonly TypeDocument TaxId = TypeDocument.Create("31", "Tax identification number");

    private static readonly TypeDocument NationalId = TypeDocument.Create("13", "National identification");

    private readonly Mock<ILicenseRepository> licenses = new();
    private readonly Mock<ITenantGrpc> tenantGrpc = new();
    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<IPaymentGrpc> paymentGrpc = new();
    private readonly Mock<IUserContext> user = new();
    private readonly Mock<IPubSub> pubSub = new();
    private readonly LicenseAggregate license;

    public PayOrderTenantDocumentTest()
    {
        MapsterConfigLicense.Configure();

        license = LicenseAggregate.Create(
            Guid.NewGuid(), "Esencial", "Short", "Description", [],
            [Price.Create(BillingType.Monthly, Money.FromDecimal(100, "COP", 2), BillingModel.FlatRate, 0, TaxTreatment.Taxed, 1900, null)],
            Icon.Create("solar:fire-bold", "#EC4899"), "Terms", [], isActive: true, isPopular: false, showInLandingPage: true, "Invoice concept", [], Guid.NewGuid());

        user.SetupGet(x => x.IdUser).Returns(Guid.NewGuid());
        licenses.Setup(x => x.FindAsync<LicenseAggregate>(license.Id, It.IsAny<CancellationToken>())).ReturnsAsync(license);
        orders.Setup(x => x.CreateIfAbsentAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        paymentGrpc
            .Setup(x => x.InitiatePaymentAsync(It.IsAny<GrpcPayment.InitiatePaymentRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GrpcPayment.InitiatePaymentResponse { PaymentId = Guid.NewGuid().ToString(), Success = true, NextAction = GrpcPayment.NextActionType.Redirect, RedirectUrl = "https://checkout.wompi.test/l/744" });
    }

    private PayOrderCommandHandler Handler(IOptions<TenantDocumentOptions> tenantDocument)
    {
        return new PayOrderCommandHandler(
            licenses.Object,
            user.Object,
            pubSub.Object,
            new Mapper(),
            paymentGrpc.Object,
            tenantGrpc.Object,
            orders.Object,
            Microsoft.Extensions.Options.Options.Create(new OnlinePaymentOptions { ReturnUrl = "https://www.kappali.test/purchase/payment-return" }),
            Mock.Of<IPackageRepository>(),
            tenantDocument);
    }

    private PayOrderCommand Command(TypeDocument typeDocument, string numberDocument)
    {
        var buyer = new BuyerDto
        {
            Name = "Buyer",
            Phone = "+573001234567",
            Email = "buyer@example.com",
            TypeDocument = NationalId,
            Document = "1023456781",
        };

        return new PayOrderCommand(Guid.NewGuid(), buyer, new LicenseDto { Id = license.Id, BillingType = BillingType.Monthly }, TestOrders.BuildTenant(typeDocument, numberDocument), "2026-11-02");
    }

    [Theory]
    [InlineData("900765432", "281")]
    [InlineData("901888777-5", "282")]
    [InlineData("901.888.777-3", "283")]
    [InlineData("9018887773", "284")]
    public async Task Handle_TenantTaxIdNotWrittenWithItsCheckDigit_RejectsBeforeTheOrderAndTheCharge(string numberDocument, string code)
    {
        // Arrange
        var handler = this.Handler(Configured);

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => handler.Handle(this.Command(TaxId, numberDocument), CancellationToken.None));

        // Assert
        Assert.Equal(code, exception.Code);
        Assert.Equal(Layer.Domain, exception.Layer);
        orders.Verify(x => x.CreateIfAbsentAsync(It.IsAny<OrderAggregate>(), It.IsAny<CancellationToken>()), Times.Never);
        paymentGrpc.VerifyNoOtherCalls();
        tenantGrpc.VerifyNoOtherCalls();
        licenses.VerifyNoOtherCalls();
        pubSub.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_TenantTaxIdWithoutDash_SaysTheDigit()
    {
        // Arrange
        var handler = this.Handler(Configured);

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => handler.Handle(this.Command(TaxId, "900765432"), CancellationToken.None));

        // Assert
        Assert.Equal("Este tipo de documento se escribe con guion y dígito de verificación. Si 900765432 es el número sin el dígito, escríbelo así: 900765432-6.", exception.Error!.Value.GetMessage("es"));
    }

    /// <summary>
    /// Sin longitud fija: nueve dígitos antes del guion en una persona jurídica; la cédula, de seis a diez, en una natural.
    /// </summary>
    [Theory]
    [InlineData("901777333-0")]
    [InlineData("79456123-2")]
    [InlineData("1023456781-6")]
    public async Task Handle_TenantTaxIdWithItsCheckDigit_CreatesTheOrderAndCharges(string numberDocument)
    {
        // Arrange
        var handler = this.Handler(Configured);

        // Act
        await handler.Handle(this.Command(TaxId, numberDocument), CancellationToken.None);

        // Assert
        orders.Verify(x => x.CreateIfAbsentAsync(It.Is<OrderAggregate>(order => order.TenantDetail.NumberDocument == numberDocument), It.IsAny<CancellationToken>()), Times.Once);
        paymentGrpc.Verify(x => x.InitiatePaymentAsync(It.IsAny<GrpcPayment.InitiatePaymentRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TenantDocumentTypeWithoutCheckDigit_AcceptsAnyNumber()
    {
        // Arrange
        var handler = this.Handler(Configured);

        // Act
        await handler.Handle(this.Command(NationalId, "900765432"), CancellationToken.None);

        // Assert
        paymentGrpc.Verify(x => x.InitiatePaymentAsync(It.IsAny<GrpcPayment.InitiatePaymentRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_NoTypesConfigured_AcceptsAnyNumber()
    {
        // Arrange
        var handler = this.Handler(Microsoft.Extensions.Options.Options.Create(new TenantDocumentOptions()));

        // Act
        await handler.Handle(this.Command(TaxId, "900765432"), CancellationToken.None);

        // Assert
        paymentGrpc.Verify(x => x.InitiatePaymentAsync(It.IsAny<GrpcPayment.InitiatePaymentRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// El reintento de una orden ya guardada no se vuelve a validar: recibe la respuesta del cobro que ya se inició.
    /// </summary>
    [Fact]
    public async Task Handle_RetryOfAStoredOrderWithATaxIdWithoutDash_ReturnsThePaymentAlreadyInitiated()
    {
        // Arrange
        var command = this.Command(TaxId, "900765432");
        var stored = OrderAggregate.Create(command.Id, Guid.NewGuid(), TestOrders.BuildLicense(), SubscriptionFixtures.Buyer(), command.TenantDetail, user.Object.IdUser);

        orders.Setup(x => x.FindAsync<OrderAggregate>(command.Id, It.IsAny<CancellationToken>())).ReturnsAsync(stored);

        // Act
        var response = await this.Handler(Configured).Handle(command, CancellationToken.None);

        // Assert
        Assert.Equal(stored.PaymentId, response.PaymentId);
    }

    /// <summary>
    /// Una orden guardada antes de la regla se sigue leyendo: la copia de la copropiedad se hidrata sin validar el dígito.
    /// </summary>
    [Fact]
    public void Deserialize_TenantStoredBeforeTheRule_Hydrates()
    {
        // Arrange
        var json = JsonSerializer.Serialize(TestOrders.BuildTenant(TaxId, "900765432"));

        // Act
        var tenant = JsonSerializer.Deserialize<TenantSnapshot>(json);

        // Assert
        Assert.Equal("900765432", tenant!.NumberDocument);
    }

    /// <summary>
    /// La renovación no escribe el documento de la copropiedad, lo toma como está: con uno guardado antes de la regla la
    /// orden se crea igual.
    /// </summary>
    [Fact]
    public void CreateRenewal_TenantStoredBeforeTheRule_CreatesTheOrder()
    {
        // Arrange
        var license = TestOrders.BuildLicense();
        var charge = SubscriptionCharge.Compute(license.Total, Money.Zero("COP"), Money.Zero("COP"));

        // Act
        var order = OrderAggregate.CreateRenewal(Guid.NewGuid(), Guid.NewGuid(), license, SubscriptionFixtures.Buyer(), TestOrders.BuildTenant(TaxId, "900765432"), charge, TestOrders.BuyerId);

        // Assert
        Assert.Equal("900765432", order.TenantDetail.NumberDocument);
    }

    /// <summary>
    /// Decisión del plan: renovar, cambiar de licencia y comprar paquetes no validan el documento de la copropiedad, así
    /// que no reciben las reglas. Si alguno las pide, es que empezó a validarlo y hay que revisar la decisión.
    /// </summary>
    [Theory]
    [InlineData(typeof(RenewSubscriptionCommandHandler))]
    [InlineData(typeof(ChangeSubscriptionCommandHandler))]
    [InlineData(typeof(PurchasePackageCommandHandler))]
    public void Constructor_HandlersOfAnExistingTenant_DoNotTakeTheDocumentRules(Type handler)
    {
        // Act
        var parameters = handler.GetConstructors().SelectMany(x => x.GetParameters()).Select(x => x.ParameterType);

        // Assert
        Assert.DoesNotContain(typeof(IOptions<TenantDocumentOptions>), parameters);
    }

    [Fact]
    public void Validate_TypesWithoutWeights_Fails()
    {
        // Arrange
        var options = new TenantDocumentOptions { CheckDigitDocumentTypes = ["31"] };

        // Act
        var result = new TenantDocumentOptionsValidator().Validate(null, options);

        // Assert
        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_WeightThatIsNotPositive_Fails()
    {
        // Arrange
        var options = new TenantDocumentOptions { CheckDigitDocumentTypes = ["31"], CheckDigitWeights = [3, 0, 13] };

        // Act
        var result = new TenantDocumentOptionsValidator().Validate(null, options);

        // Assert
        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_EmptyDocumentTypeCode_Fails()
    {
        // Arrange
        var options = new TenantDocumentOptions { CheckDigitDocumentTypes = [" "], CheckDigitWeights = [3, 7] };

        // Act
        var result = new TenantDocumentOptionsValidator().Validate(null, options);

        // Assert
        Assert.True(result.Failed);
    }

    [Fact]
    public void Validate_NoSection_Succeeds()
    {
        // Act
        var result = new TenantDocumentOptionsValidator().Validate(null, new TenantDocumentOptions());

        // Assert
        Assert.True(result.Succeeded);
    }

    /// <summary>
    /// El valor desplegado. ms-tenants tiene esta misma prueba con el mismo valor esperado: si una de las dos
    /// configuraciones cambia sin la otra, su prueba falla. La copropiedad que esta compra cobra la tiene que poder crear
    /// ms-tenants.
    /// </summary>
    [Fact]
    public void Appsettings_TenantDocument_IsTheSharedDeployedValue()
    {
        // Arrange
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !System.IO.File.Exists(Path.Combine(directory.FullName, "CodeDesignPlus.Net.Microservice.Licenses.sln")))
        {
            directory = directory.Parent;
        }

        var configuration = new ConfigurationBuilder().AddJsonFile(Path.Combine(directory!.FullName, "src", "entrypoints", "CodeDesignPlus.Net.Microservice.Licenses.Rest", "appsettings.json")).Build();

        // Act
        var options = configuration.GetSection(TenantDocumentOptions.Section).Get<TenantDocumentOptions>();

        // Assert
        Assert.NotNull(options);
        Assert.True(new TenantDocumentOptionsValidator().Validate(null, options).Succeeded);
        Assert.Equal(["31"], options.CheckDigitDocumentTypes);
        Assert.Equal([3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71], options.CheckDigitWeights);
    }
}
