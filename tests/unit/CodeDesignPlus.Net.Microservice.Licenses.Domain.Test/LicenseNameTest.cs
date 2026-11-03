using CodeDesignPlus.Net.Core.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Xunit;
using ProvisioningStep = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.ProvisioningStep;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test;

/// <summary>
/// El nombre de una licencia sigue una sola regla, en el catálogo y en la copia que se aplica a la copropiedad, y un pago
/// aprobado queda registrado aunque la licencia no se pueda aplicar (pendings/738).
/// </summary>
public class LicenseNameTest
{
    private static readonly Instant Start = Instant.FromUtc(2026, 11, 3, 11, 59);

    private static LicenseAggregate CatalogLicense(string name) => LicenseAggregate.Create(
        Guid.NewGuid(), name, "Short", "Description", [],
        [Price.Create(BillingType.Monthly, Money.FromLong(70_000_000, "COP"), BillingModel.FlatRate, 0, TaxTreatment.Taxed, 1900, null)],
        Icon.Create("solar:fire-bold", "#EC4899"), "Terms", [], isActive: true, isPopular: false, showInLandingPage: true, "Suscripción al servicio en la nube Kappali (SaaS)", [], Guid.NewGuid());

    /// <summary>
    /// Los nombres comerciales que vende el equipo: con «·», dígitos, «+», guiones, paréntesis, barra y tildes.
    /// </summary>
    [Theory]
    [InlineData("Élite Plus · Conjunto Prueba Licencias")]
    [InlineData("Plan 2026")]
    [InlineData("Élite+")]
    [InlineData("Estándar - Torre (A/B)")]
    [InlineData("Básico, Parqueaderos & Más.")]
    [InlineData("Pequeña copropiedad d'Ávila")]
    public void IsValid_ACommercialName_IsTrue(string name)
    {
        // Arrange
        var candidate = name;

        // Act
        var valid = LicenseName.IsValid(candidate);

        // Assert
        Assert.True(valid);
    }

    /// <summary>
    /// Vacío, solo signos, caracteres fuera de la regla o más largo que el estándar de un nombre no es un nombre de licencia.
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("+++")]
    [InlineData("Élite ★")]
    [InlineData("<b>Élite</b>")]
    [InlineData("Élite\nPlus")]
    public void IsValid_ANameOutsideTheRule_IsFalse(string name)
    {
        // Arrange
        var candidate = name;

        // Act
        var valid = LicenseName.IsValid(candidate);

        // Assert
        Assert.False(valid);
    }

    /// <summary>
    /// La longitud es la estándar de un nombre (regla 48): cabe justo, y uno más ya no.
    /// </summary>
    [Fact]
    public void IsValid_TheStandardNameLength_IsTheLimit()
    {
        // Arrange
        var longest = new string('a', FieldLength.Name);

        // Act
        var valid = (LicenseName.IsValid(longest), LicenseName.IsValid(longest + "a"));

        // Assert
        Assert.Equal((true, false), valid);
    }

    /// <summary>
    /// El catálogo rechaza al crear lo que después no se podría aplicar a una copropiedad.
    /// </summary>
    [Fact]
    public void Create_ANameThatCannotBeApplied_IsRejected()
    {
        // Arrange
        var name = "Élite ★";

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => CatalogLicense(name));

        // Assert
        Assert.Equal(Errors.LicenseNameIsInvalid.Code, exception.Code);
    }

    /// <summary>
    /// Editar tampoco deja poner un nombre que no se podría aplicar.
    /// </summary>
    [Fact]
    public void Update_ANameThatCannotBeApplied_IsRejected()
    {
        // Arrange
        var license = CatalogLicense("Élite");

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => license.Update(
            "Élite ★", license.ShortDescription, license.Description, license.Modules, license.Prices, license.Icon, license.TermsOfService,
            license.Attributes, license.IsActive, license.IsPopular, license.ShowInLandingPage, license.InvoiceConcept!, [], Guid.NewGuid()));

        // Assert
        Assert.Equal(Errors.LicenseNameIsInvalid.Code, exception.Code);
    }

    /// <summary>
    /// La copia que se aplica a la copropiedad acepta los nombres que el catálogo acepta: antes «Élite Plus · …», «Plan 2026»
    /// y «Élite+» lanzaban el error 192 al aplicar el pago.
    /// </summary>
    [Theory]
    [InlineData("Élite Plus · Conjunto Prueba Licencias")]
    [InlineData("Plan 2026")]
    [InlineData("Élite+")]
    public void LicenseTenantCreate_ANameTheCatalogAccepts_IsAccepted(string name)
    {
        // Arrange
        var catalog = CatalogLicense(name);

        // Act
        var tenantLicense = LicenseTenant.Create(catalog.Id, catalog.Name, Start, Start.Plus(Duration.FromDays(30)), [], []);

        // Assert
        Assert.Equal(name, tenantLicense.Name);
    }

    /// <summary>
    /// Un pago aprobado de una orden cuya licencia no se puede aplicar queda registrado: la orden pasa a pagada, con el
    /// periodo, en <see cref="ProvisioningStatus.PartiallyFailed"/>, con el motivo en el historial y el aviso de
    /// aprovisionamiento fallido. Antes la excepción dejaba la orden sin pagar y el pago perdido.
    /// </summary>
    [Fact]
    public void SetPaymentStatus_PaidButTheLicenseCannotBeApplied_RecordsThePaymentAndTheFailure()
    {
        // Arrange
        var order = OrderAggregate.Create(OrdenDePrueba.OrderId, OrdenDePrueba.PaymentId, OrdenDePrueba.Licencia("Élite ★"), OrdenDePrueba.Comprador(), OrdenDePrueba.Copropiedad(), OrdenDePrueba.UserId);

        // Act
        order.SetPaymentStatus(PaymentStatus.Succeeded, OrdenDePrueba.BuyerId, Start);

        // Assert
        var failed = order.ProvisioningHistory[^1];
        var events = order.GetAndClearEvents();
        Assert.Equal(
            (PaymentStatus.Succeeded, true, ProvisioningStatus.PartiallyFailed, ProvisioningStep.LicenseApplication, ProvisioningStepStatus.Failed, true, true, 1, true),
            (order.PaymentStatus, order.IsSuccess, order.ProvisioningStatus, failed.StepName, failed.Status, failed.Error!.StartsWith(Errors.LicenseNameIsInvalid.Code), order.LicenseApplicationFailed, events.Count, events[0] is OrderProvisioningFailedDomainEvent));
    }

    /// <summary>
    /// Con un nombre que se puede aplicar, el pago sigue su camino de siempre y no deja rastro de fallo.
    /// </summary>
    [Fact]
    public void SetPaymentStatus_PaidWithAnApplicableName_PublishesTheProvisioning()
    {
        // Arrange
        var order = OrderAggregate.Create(OrdenDePrueba.OrderId, OrdenDePrueba.PaymentId, OrdenDePrueba.Licencia("Élite Plus · Conjunto Prueba Licencias"), OrdenDePrueba.Comprador(), OrdenDePrueba.Copropiedad(), OrdenDePrueba.UserId);

        // Act
        order.SetPaymentStatus(PaymentStatus.Succeeded, OrdenDePrueba.BuyerId, Start);

        // Assert
        var events = order.GetAndClearEvents();
        Assert.Equal(
            (ProvisioningStatus.InProgress, false, true),
            (order.ProvisioningStatus, order.LicenseApplicationFailed, events.Single() is OrderPaidAndReadyForProvisioningDomainEvent));
    }
}
