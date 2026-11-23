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

    [Theory]
    [InlineData("")]
    [InlineData("Conjunto Residencial Las Acacias de la Sabana Propiedad Horizontal Etapa Primera Etapa Segunda Etapa Tercera y Zonas Comunes Generales")]
    public async Task TakeAsync_NameTheOrderDoesNotAccept_SaysWhichDataToCorrectAndWhere(string name)
    {
        // Arrange
        var answer = SubscriptionFixtures.AnswerOf(Current());
        answer.Name = name;

        // Act
        var exception = await FailsWithAsync(answer);

        // Assert
        Assert.Equal(
            (Errors.TenantNameIsNotValid.GetCode(), $"El nombre de tu copropiedad, '{name}', no es válido: está vacío o es demasiado largo. Corrígelo en Organización y vuelve a intentarlo."),
            (exception.Code, exception.Error!.Value.GetMessage("es")));
    }

    [Fact]
    public async Task TakeAsync_WebTheOrderDoesNotAccept_SaysWhichDataToCorrectAndWhere()
    {
        // Arrange
        var answer = SubscriptionFixtures.AnswerOf(Current());
        answer.Domain = "acacias sin dominio";

        // Act
        var exception = await FailsWithAsync(answer);

        // Assert
        Assert.Equal(
            (Errors.TenantWebIsNotValid.GetCode(), "El sitio web de tu copropiedad, 'acacias sin dominio', no es válido. Corrígelo en Organización y vuelve a intentarlo."),
            (exception.Code, exception.Error!.Value.GetMessage("es")));
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task TakeAsync_TenantWithoutDocument_SaysToCompleteIt(bool withoutType, bool withoutNumber)
    {
        // Arrange
        var answer = SubscriptionFixtures.AnswerOf(Current());
        answer.TypeDocument = withoutType ? null : answer.TypeDocument;
        answer.NumberDocument = withoutNumber ? string.Empty : answer.NumberDocument;

        // Act
        var exception = await FailsWithAsync(answer);

        // Assert
        Assert.Equal(
            (Errors.TenantDocumentIsMissing.GetCode(), "A tu copropiedad le falta el tipo o el número de documento. Complétalo en Organización y vuelve a intentarlo."),
            (exception.Code, exception.Error!.Value.GetMessage("es")));
    }

    [Theory]
    [InlineData("", "110111")]
    [InlineData("Carrera 7 # 71-21", "")]
    public async Task TakeAsync_TenantWithoutAddressOrPostalCode_SaysToCompleteThem(string address, string postalCode)
    {
        // Arrange
        var answer = SubscriptionFixtures.AnswerOf(Current());
        answer.Location.Address = address;
        answer.Location.PostalCode = postalCode;

        // Act
        var exception = await FailsWithAsync(answer);

        // Assert
        Assert.Equal(
            (Errors.TenantAddressIsMissing.GetCode(), "A tu copropiedad le falta la dirección o el código postal. Complétalos en Organización y vuelve a intentarlo."),
            (exception.Code, exception.Error!.Value.GetMessage("es")));
    }

    /// <summary>
    /// The failure of production on 2026-11-23: ms-tenants answered the currency of the country without its id, because its
    /// mapping skipped it. The data of the copropiedad were right, so the error does not send it to review them: it says
    /// the problem is of the platform and which part of the answer failed.
    /// </summary>
    [Fact]
    public async Task TakeAsync_CurrencyWithoutItsId_SaysTheProblemIsOfThePlatformAndWhichPartFailed()
    {
        // Arrange
        var answer = SubscriptionFixtures.AnswerAsMsTenantsSendsIt();
        answer.Location.Country.Currency.Id = string.Empty;

        // Act
        var exception = await FailsWithAsync(answer);

        // Assert
        Assert.Equal(
            (Errors.TenantCouldNotBeRead.GetCode(), "No pudimos preparar la orden por un problema de la plataforma, no de los datos de tu copropiedad. No tienes que corregir nada: avisa a soporte con este detalle: location.country.currency: CurrencyId is empty"),
            (exception.Code, exception.Error!.Value.GetMessage("es")));
    }

    /// <summary>
    /// What comes from the catalogs of the platform is not corrected by the copropiedad: whichever part is missing or
    /// broken, the error names it and never says «Organización».
    /// </summary>
    [Theory]
    [InlineData("location", "location is missing")]
    [InlineData("country", "location.country is missing")]
    [InlineData("currency", "location.country.currency is missing")]
    [InlineData("state", "location.state is missing")]
    [InlineData("city", "location.city is missing")]
    [InlineData("country.id", "location.country: ")]
    [InlineData("country.timezone", "location.country: ")]
    [InlineData("currency.code", "location.country.currency: ")]
    [InlineData("state.id", "location.state: ")]
    [InlineData("city.id", "location.city: ")]
    [InlineData("locality.name", "location.locality: ")]
    public async Task TakeAsync_PartOfTheLocationThatComesFromTheCatalogsBroken_SaysTheProblemIsOfThePlatform(string broken, string detail)
    {
        // Arrange
        var answer = SubscriptionFixtures.AnswerAsMsTenantsSendsIt();
        var location = answer.Location;

        switch (broken)
        {
            case "location": answer.Location = null; break;
            case "country": location.Country = null; break;
            case "currency": location.Country.Currency = null; break;
            case "state": location.State = null; break;
            case "city": location.City = null; break;
            case "country.id": location.Country.Id = string.Empty; break;
            case "country.timezone": location.Country.Timezone = string.Empty; break;
            case "currency.code": location.Country.Currency.Code = string.Empty; break;
            case "state.id": location.State.Id = string.Empty; break;
            case "city.id": location.City.Id = "not-a-guid"; break;
            default: location.Locality.Name = string.Empty; break;
        }

        // Act
        var exception = await FailsWithAsync(answer);
        var message = exception.Error!.Value.GetMessage("es");

        // Assert
        Assert.Equal(
            (Errors.TenantCouldNotBeRead.GetCode(), true, false),
            (exception.Code, message.Contains("avisa a soporte con este detalle: " + detail, StringComparison.Ordinal), message.Contains("Organización", StringComparison.Ordinal)));
    }

    /// <summary>
    /// The answer with the shape ms-tenants gives it (its mapping from TenantDto): the website as a Uri prints it, the
    /// phone code and the time zones of the catalogs, the license with its modules. Every field the order keeps is read.
    /// </summary>
    [Fact]
    public async Task TakeAsync_AnswerAsMsTenantsSendsIt_BuildsTheWholeTenant()
    {
        // Arrange
        var answer = SubscriptionFixtures.AnswerAsMsTenantsSendsIt();
        grpc.Has(answer);

        // Act
        var tenant = await Snapshots().TakeAsync(TestOrders.TenantId, CancellationToken.None);

        // Assert
        var location = tenant.Location;

        Assert.Equal(
            ("Conjunto Las Acacias", "31", "NIT", "901777333-0", "https://acacias.example.com/", "+573009876543", "administracion@acacias.example.com"),
            (tenant.Name, tenant.TypeDocument.Code, tenant.TypeDocument.Name, tenant.NumberDocument, tenant.Web, tenant.Phone, tenant.Email));
        Assert.Equal(
            (Guid.Parse(answer.Location.Country.Currency.Id), "COP", "Peso Colombiano", "$", (short)2, (short)170),
            (location.Country.Currency.CurrencyId, location.Country.Currency.Code, location.Country.Currency.Name, location.Country.Currency.Symbol, location.Country.Currency.DecimalDigits, location.Country.Currency.NumericCode));
        Assert.Equal(
            (Guid.Parse(answer.Location.Country.Id), "Colombia", "CO", "COL", (ushort)170, "+57", "America/Bogota"),
            (location.Country.Id, location.Country.Name, location.Country.Alpha2, location.Country.Alpha3, location.Country.Code, location.Country.PhoneCode, location.Country.Timezone));
        Assert.Equal(
            (Guid.Parse(answer.Location.State.Id), "Bogotá, D.C.", "11", Guid.Parse(answer.Location.City.Id), "Bogotá, D.C.", "America/Bogota"),
            (location.State.Id, location.State.Name, location.State.Code, location.City.Id, location.City.Name, location.City.Timezone));
        Assert.Equal(
            (Guid.Parse(answer.Location.Locality.Id), "Chapinero", null, "Carrera 7 # 71-21", "110111"),
            (location.Locality!.Id, location.Locality.Name, location.Neighborhood, location.Address, location.PostalCode));
    }

    /// <summary>
    /// The new errors are written in the four languages (rule 35), with their values in place.
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
            Errors.TenantPhoneIsNotValid.With("some-value").GetMessage(language),
            Errors.TenantEmailIsNotValid.With("some-value").GetMessage(language),
            Errors.TenantNameIsNotValid.With("some-value").GetMessage(language),
            Errors.TenantWebIsNotValid.With("some-value").GetMessage(language),
            Errors.TenantCouldNotBeRead.With("some-value").GetMessage(language),
            Errors.TenantDocumentIsMissing.GetMessage(language),
            Errors.TenantAddressIsMissing.GetMessage(language)
        ];

        // Assert
        Assert.Equal(
            (8, 5, false),
            (new System.Collections.Generic.HashSet<string>(messages).Count, Array.FindAll(messages, x => x.Contains("some-value", StringComparison.Ordinal)).Length, Array.Exists(messages, x => x.Length < 20 || x.Contains("{0}", StringComparison.Ordinal))));
    }

    /// <summary>
    /// Only what the copropiedad writes sends it to «Organización»; the failure of the platform does not, in any language.
    /// </summary>
    [Theory]
    [InlineData("es", "Organización")]
    [InlineData("en", "Organization")]
    [InlineData("fr", "Organisation")]
    [InlineData("pt", "Organização")]
    public void Errors_ProblemOfThePlatform_DoesNotSendToCorrectTheData(string language, string screen)
    {
        // Act
        var message = Errors.TenantCouldNotBeRead.With("location.country.currency: CurrencyId is empty").GetMessage(language);

        // Assert
        Assert.Equal((false, true), (message.Contains(screen, StringComparison.Ordinal), message.EndsWith("location.country.currency: CurrencyId is empty", StringComparison.Ordinal)));
    }
}
