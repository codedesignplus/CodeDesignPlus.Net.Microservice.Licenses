using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.ValueObjects.User;
using Grpc.Core;
using Xunit;
using TenantContracts = CodeDesignPlus.Net.gRpc.Clients.Services.Tenant;
using TenantSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.Tenant;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;

/// <summary>
/// The tenant an order that is not the first purchase keeps is the one ms-tenants has today (Kappali pendings/744).
/// </summary>
public class TenantSnapshotsTest
{
    private static readonly TypeDocument TaxId = TypeDocument.Create("31", "NIT");

    private readonly Mock<ITenantGrpc> grpc = new();

    private static TenantSnapshot Current(string numberDocument = "901777333-0") => TenantSnapshot.Create(
        TestOrders.TenantId, "Conjunto Las Acacias", "https://acacias.example.com", TaxId, numberDocument, "+573009876543", "administracion@acacias.example.com", TestOrders.BuildTenant().Location);

    private TenantSnapshots Snapshots() => new(grpc.Object);

    private async Task<CodeDesignPlusException> FailsWithAsync(TenantContracts.GetTenantResponse answer)
    {
        grpc.Has(answer);

        return await Assert.ThrowsAsync<CodeDesignPlusException>(() => Snapshots().TakeAsync(TestOrders.TenantId, CancellationToken.None));
    }

    [Fact]
    public async Task TakeAsync_TenantInMsTenants_CopiesWhatItHasToday()
    {
        // Arrange
        grpc.Has(SubscriptionFixtures.AnswerOf(Current()));

        // Act
        var tenant = await Snapshots().TakeAsync(TestOrders.TenantId, CancellationToken.None);

        // Assert
        Assert.Equal(
            (TestOrders.TenantId, "Conjunto Las Acacias", "31", "901777333-0", "+573009876543", "administracion@acacias.example.com", "https://acacias.example.com"),
            (tenant.Id, tenant.Name, tenant.TypeDocument.Code, tenant.NumberDocument, tenant.Phone, tenant.Email, tenant.Web));
    }

    /// <summary>
    /// What comes back is equal to what went: an order waiting for its payment is reused only when its copy equals the
    /// current one, and a copy that never compared equal would create a new order on every click.
    /// </summary>
    [Fact]
    public async Task TakeAsync_TenantInMsTenants_EqualsTheTenantItWasBuiltFrom()
    {
        // Arrange
        grpc.Has(SubscriptionFixtures.AnswerOf(Current()));

        // Act
        var tenant = await Snapshots().TakeAsync(TestOrders.TenantId, CancellationToken.None);

        // Assert
        Assert.Equal(Current(), tenant);
    }

    [Fact]
    public async Task TakeAsync_TenantInMsTenants_KeepsItsWholeLocation()
    {
        // Arrange
        grpc.Has(SubscriptionFixtures.AnswerOf(Current()));

        // Act
        var tenant = await Snapshots().TakeAsync(TestOrders.TenantId, CancellationToken.None);

        // Assert
        Assert.Equal(
            ("COP", "America/Bogota", "America/Bogota", "Chapinero", "El Paraíso", "Carrera 7 # 71-21"),
            (tenant.Location.Country.Currency.Code, tenant.Location.Country.Timezone, tenant.Location.City.Timezone, tenant.Location.Locality!.Name, tenant.Location.Neighborhood!.Name, tenant.Location.Address));
    }

    [Fact]
    public async Task TakeAsync_TenantWithoutLocalityNorNeighborhood_LeavesThemEmpty()
    {
        // Arrange
        var answer = SubscriptionFixtures.AnswerOf(Current());
        answer.Location.Locality = null;
        answer.Location.Neighborhood = new TenantContracts.Neighborhood();
        grpc.Has(answer);

        // Act
        var tenant = await Snapshots().TakeAsync(TestOrders.TenantId, CancellationToken.None);

        // Assert
        Assert.Equal((null, null), (tenant.Location.Locality, tenant.Location.Neighborhood));
    }

    /// <summary>
    /// The tenant asked for is the one given, in the body of the request: not the one of whoever is signed in.
    /// </summary>
    [Fact]
    public async Task TakeAsync_AnyTenant_AsksMsTenantsForThatId()
    {
        // Arrange
        grpc.Has(SubscriptionFixtures.AnswerOf(Current()));

        // Act
        await Snapshots().TakeAsync(TestOrders.TenantId, CancellationToken.None);

        // Assert
        grpc.Verify(x => x.GetTenantByIdAsync(It.Is<TenantContracts.GetTenantRequest>(r => r.Id == TestOrders.TenantId.ToString()), It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// The document is copied as it is, without its check digit too: a renewal is never refused for it, the invoice waits.
    /// </summary>
    [Theory]
    [InlineData("901777333")]
    [InlineData("901777333-5")]
    [InlineData("9017773330")]
    public async Task TakeAsync_DocumentWithoutItsCheckDigit_IsCopiedAsItIs(string numberDocument)
    {
        // Arrange
        grpc.Has(SubscriptionFixtures.AnswerOf(Current(numberDocument)));

        // Act
        var tenant = await Snapshots().TakeAsync(TestOrders.TenantId, CancellationToken.None);

        // Assert
        Assert.Equal(numberDocument, tenant.NumberDocument);
    }

    [Fact]
    public async Task TakeAsync_TenantNotInMsTenants_SaysItWasNotFound()
    {
        // Arrange
        grpc.Setup(x => x.GetTenantByIdAsync(It.IsAny<TenantContracts.GetTenantRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RpcException(new Status(StatusCode.NotFound, "not found")));

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => Snapshots().TakeAsync(TestOrders.TenantId, CancellationToken.None));

        // Assert
        Assert.Equal((Errors.TenantOfTheOrderNotFound.GetCode(), Layer.Application), (exception.Code, exception.Layer));
    }

    /// <summary>
    /// ms-tenants down is not «not found»: the failure goes up as it is, and nothing is created nor charged.
    /// </summary>
    [Fact]
    public async Task TakeAsync_MsTenantsDown_FailsWithItsError()
    {
        // Arrange
        grpc.Setup(x => x.GetTenantByIdAsync(It.IsAny<TenantContracts.GetTenantRequest>(), It.IsAny<CancellationToken>())).ThrowsAsync(new RpcException(new Status(StatusCode.Unavailable, "down")));

        // Act
        var exception = await Assert.ThrowsAsync<RpcException>(() => Snapshots().TakeAsync(TestOrders.TenantId, CancellationToken.None));

        // Assert
        Assert.Equal(StatusCode.Unavailable, exception.StatusCode);
    }

    /// <summary>
    /// An answer about another tenant would invoice the order to someone else.
    /// </summary>
    [Fact]
    public async Task TakeAsync_AnswerAboutAnotherTenant_IsRefused()
    {
        // Arrange
        var answer = SubscriptionFixtures.AnswerOf(Current());
        answer.Id = Guid.NewGuid().ToString();
        grpc.Setup(x => x.GetTenantByIdAsync(It.IsAny<TenantContracts.GetTenantRequest>(), It.IsAny<CancellationToken>())).ReturnsAsync(answer);

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => Snapshots().TakeAsync(TestOrders.TenantId, CancellationToken.None));

        // Assert
        Assert.Equal(Errors.TenantOfTheOrderNotFound.GetCode(), exception.Code);
    }

    /// <summary>
    /// The phone ms-tenants has does not fit in an order: the error names the phone of the copropiedad, shows it and says
    /// where it is corrected, instead of a bare «the phone has invalid characters».
    /// </summary>
    [Theory]
    [InlineData("300 123 4567")]
    [InlineData("601-7001234")]
    [InlineData("")]
    public async Task TakeAsync_PhoneTheOrderDoesNotAccept_SaysWhichDataToCorrectAndWhere(string phone)
    {
        // Arrange
        var answer = SubscriptionFixtures.AnswerOf(Current());
        answer.Phone = phone;

        // Act
        var exception = await FailsWithAsync(answer);

        // Assert
        Assert.Equal(
            (Errors.TenantPhoneIsNotValid.GetCode(), $"El teléfono de tu copropiedad, '{phone}', no es válido: va solo con dígitos, entre 7 y 15, y puede empezar por +. Corrígelo en Organización y vuelve a intentarlo."),
            (exception.Code, exception.Error!.Value.GetMessage("es")));
    }

    [Theory]
    [InlineData("administracion-acacias")]
    [InlineData("")]
    public async Task TakeAsync_EmailTheOrderDoesNotAccept_SaysWhichDataToCorrectAndWhere(string email)
    {
        // Arrange
        var answer = SubscriptionFixtures.AnswerOf(Current());
        answer.Email = email;

        // Act
        var exception = await FailsWithAsync(answer);

        // Assert
        Assert.Equal(
            (Errors.TenantEmailIsNotValid.GetCode(), $"El correo de tu copropiedad, '{email}', no es válido. Corrígelo en Organización y vuelve a intentarlo."),
            (exception.Code, exception.Error!.Value.GetMessage("es")));
    }

    [Fact]
    public async Task TakeAsync_NameTheOrderDoesNotAccept_SaysWhereToReviewTheData()
    {
        // Arrange
        var answer = SubscriptionFixtures.AnswerOf(Current());
        answer.Name = string.Empty;

        // Act
        var exception = await FailsWithAsync(answer);

        // Assert
        Assert.Equal(
            (Errors.TenantDataIsNotValid.GetCode(), "Tu copropiedad tiene un dato incompleto o no válido: el nombre, el documento, el sitio web o la ubicación. Revísalos en Organización y vuelve a intentarlo."),
            (exception.Code, exception.Error!.Value.GetMessage("es")));
    }

    [Fact]
    public async Task TakeAsync_TenantWithoutLocation_SaysWhereToReviewTheData()
    {
        // Arrange
        var answer = SubscriptionFixtures.AnswerOf(Current());
        answer.Location = null;

        // Act
        var exception = await FailsWithAsync(answer);

        // Assert
        Assert.Equal(Errors.TenantDataIsNotValid.GetCode(), exception.Code);
    }

    /// <summary>
    /// The four new errors are written in the four languages (rule 35), with their values in place.
    /// </summary>
    [Theory]
    [InlineData("es")]
    [InlineData("en")]
    [InlineData("fr")]
    [InlineData("pt")]
    public void Errors_OfTheCurrentTenant_AreWrittenInEveryLanguage(string language)
    {
        // Act
        string[] messages =
        [
            Errors.TenantOfTheOrderNotFound.GetMessage(language),
            Errors.TenantPhoneIsNotValid.With("300 123").GetMessage(language),
            Errors.TenantEmailIsNotValid.With("a@b").GetMessage(language),
            Errors.TenantDataIsNotValid.GetMessage(language)
        ];

        // Assert
        Assert.Equal(
            (4, true, true, false),
            (new System.Collections.Generic.HashSet<string>(messages).Count, messages[1].Contains("300 123", StringComparison.Ordinal), messages[2].Contains("a@b", StringComparison.Ordinal), Array.Exists(messages, x => x.Length < 20 || x.Contains("{0}", StringComparison.Ordinal))));
    }
}
