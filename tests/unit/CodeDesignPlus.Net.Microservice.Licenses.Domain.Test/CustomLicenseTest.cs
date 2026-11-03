using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test;

/// <summary>
/// Las licencias a medida (pendings/464): una licencia que no se ve en la landing solo se le ofrece a las copropiedades que
/// eligió el equipo comercial, con quién se la ofreció y cuándo.
/// </summary>
public class CustomLicenseTest
{
    private static readonly Guid LasAcacias = Guid.Parse("46400000-0000-4000-8000-000000000001");
    private static readonly Guid LosCerezos = Guid.Parse("46400000-0000-4000-8000-000000000002");
    private static readonly Guid Seller = Guid.Parse("46400000-0000-4000-8000-000000000003");

    private static LicenseAggregate Create(bool showInLandingPage, List<OfferedTenant> offeredTo, bool isActive = true) => LicenseAggregate.Create(
        Guid.NewGuid(), "Élite a medida", "Short", "Description", [],
        [Price.Create(BillingType.Monthly, Money.FromLong(50_000_000, "COP"), BillingModel.FlatRate, 0, TaxTreatment.Excluded, 0, "Art. 476 num. 21 ET")],
        Icon.Create("solar:fire-bold", "#EC4899"), "Terms", [], isActive, isPopular: false, showInLandingPage, "Suscripción al servicio en la nube Kappali (SaaS)", offeredTo, Seller);

    private static void Update(LicenseAggregate license, List<OfferedTenant> offeredTo, Guid updatedBy) => license.Update(
        license.Name, license.ShortDescription, license.Description, license.Modules, license.Prices, license.Icon, license.TermsOfService,
        license.Attributes, license.IsActive, license.IsPopular, license.ShowInLandingPage, license.InvoiceConcept!, offeredTo, updatedBy);

    /// <summary>
    /// La licencia privada se le ofrece a la copropiedad elegida y a ninguna otra.
    /// </summary>
    [Fact]
    public void IsOfferedTo_APrivateLicense_OnlyTheTenantItWasOfferedTo()
    {
        // Arrange
        var license = Create(showInLandingPage: false, [new OfferedTenant(LasAcacias, "Las Acacias")]);

        // Act
        var offered = (license.IsOfferedTo(LasAcacias), license.IsOfferedTo(LosCerezos));

        // Assert
        Assert.Equal((true, false), offered);
    }

    /// <summary>
    /// Una licencia privada no está a la venta: la compra inicial no la acepta aunque se la hayan ofrecido a alguien.
    /// </summary>
    [Fact]
    public void IsForSale_APrivateLicense_IsFalse()
    {
        // Arrange
        var license = Create(showInLandingPage: false, [new OfferedTenant(LasAcacias, "Las Acacias")]);

        // Act
        var forSale = license.IsForSale;

        // Assert
        Assert.False(forSale);
    }

    /// <summary>
    /// Una licencia privada inactiva no se le ofrece ni a la copropiedad elegida.
    /// </summary>
    [Fact]
    public void IsOfferedTo_AnInactivePrivateLicense_IsFalse()
    {
        // Arrange
        var license = Create(showInLandingPage: false, [new OfferedTenant(LasAcacias, "Las Acacias")], isActive: false);

        // Act
        var offered = license.IsOfferedTo(LasAcacias);

        // Assert
        Assert.False(offered);
    }

    /// <summary>
    /// Una licencia pública ya se ofrece a todas: llevar copropiedades sería decir dos cosas a la vez.
    /// </summary>
    [Fact]
    public void Create_APublicLicenseWithTenants_IsRejected()
    {
        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => Create(showInLandingPage: true, [new OfferedTenant(LasAcacias, "Las Acacias")]));

        // Assert
        Assert.Equal(Errors.PublicLicenseCannotBeOfferedToTenants.GetCode(), exception.Code);
    }

    /// <summary>
    /// Una copropiedad no se repite en la lista.
    /// </summary>
    [Fact]
    public void Create_TheSameTenantTwice_IsRejected()
    {
        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => Create(showInLandingPage: false, [new OfferedTenant(LasAcacias, "Las Acacias"), new OfferedTenant(LasAcacias, "Las Acacias")]));

        // Assert
        Assert.Equal(Errors.TenantIsOfferedTwice.GetCode(), exception.Code);
    }

    /// <summary>
    /// Al ofrecerla queda quién y cuándo, y sale el evento para avisar a la copropiedad.
    /// </summary>
    [Fact]
    public void Create_APrivateLicense_RecordsWhoOfferedItAndPublishesTheOffer()
    {
        // Act
        var license = Create(showInLandingPage: false, [new OfferedTenant(LasAcacias, "Las Acacias")]);

        // Assert
        var offer = Assert.Single(license.OfferedTo!);
        var @event = Assert.Single(license.GetAndClearEvents().OfType<LicenseOfferedDomainEvent>());
        Assert.Equal((LasAcacias, Seller, LasAcacias, Seller), (offer.TenantId, offer.OfferedBy, @event.Tenant, @event.OfferedBy));
    }

    /// <summary>
    /// Editar la licencia no reescribe quién ofreció la oferta que ya estaba ni cuándo; la nueva queda con quien edita y
    /// es la única que se avisa.
    /// </summary>
    [Fact]
    public void Update_KeepsWhoAndWhenOfTheExistingOfferAndAnnouncesOnlyTheNewOne()
    {
        // Arrange
        var license = Create(showInLandingPage: false, [new OfferedTenant(LasAcacias, "Las Acacias")]);
        var original = Assert.Single(license.OfferedTo!);
        license.GetAndClearEvents();
        var editor = Guid.NewGuid();

        // Act
        Update(license, [new OfferedTenant(LasAcacias, "Las Acacias"), new OfferedTenant(LosCerezos, "Los Cerezos")], editor);

        // Assert
        var acacias = license.OfferedTo!.Single(x => x.TenantId == LasAcacias);
        var cerezos = license.OfferedTo!.Single(x => x.TenantId == LosCerezos);
        var announced = string.Join(",", license.GetAndClearEvents().OfType<LicenseOfferedDomainEvent>().Select(x => x.Tenant));
        Assert.Equal((original.OfferedBy, original.OfferedAt, editor, LosCerezos.ToString()), (acacias.OfferedBy, acacias.OfferedAt, cerezos.OfferedBy, announced));
    }

    /// <summary>
    /// Quitar la copropiedad de la lista le retira la oferta.
    /// </summary>
    [Fact]
    public void Update_WithoutTheTenant_WithdrawsTheOffer()
    {
        // Arrange
        var license = Create(showInLandingPage: false, [new OfferedTenant(LasAcacias, "Las Acacias")]);

        // Act
        Update(license, [], Guid.NewGuid());

        // Assert
        Assert.False(license.IsOfferedTo(LasAcacias));
    }

    /// <summary>
    /// El concepto de la factura es obligatorio (pendings/662).
    /// </summary>
    [Fact]
    public void Create_WithoutInvoiceConcept_IsRejected()
    {
        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => LicenseAggregate.Create(
            Guid.NewGuid(), "Esencial", "Short", "Description", [],
            [Price.Create(BillingType.Monthly, Money.FromLong(12_000_000, "COP"), BillingModel.FlatRate, 0, TaxTreatment.Taxed, 1900, null)],
            Icon.Create("solar:fire-bold", "#EC4899"), "Terms", [], true, false, true, " ", [], Seller));

        // Assert
        Assert.Equal(Errors.InvoiceConceptIsRequired.GetCode(), exception.Code);
    }

    /// <summary>
    /// Una licencia guardada antes de existir las ofertas y el concepto de factura se sigue leyendo (regla 31), y como
    /// pública se le sigue ofreciendo a todas.
    /// </summary>
    [Fact]
    public void Deserialize_ALicenseStoredBeforeOffersAndInvoiceConcept_StillReadsAndIsOffered()
    {
        // Arrange
        CodeDesignPlus.Net.Mongo.Extensions.MongoSerializerRegistration.RegisterSerializers();

        var document = Create(showInLandingPage: true, []).ToBsonDocument();

        document.Remove("OfferedTo");
        document.Remove("InvoiceConcept");

        // Act
        var stored = BsonSerializer.Deserialize<LicenseAggregate>(document);

        // Assert
        Assert.Equal((true, (string?)null), (stored.IsOfferedTo(LasAcacias), stored.InvoiceConcept));
    }
}
