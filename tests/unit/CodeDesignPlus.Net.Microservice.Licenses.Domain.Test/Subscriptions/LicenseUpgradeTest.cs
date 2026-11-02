using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using static CodeDesignPlus.Net.Microservice.Licenses.Domain.Test.Subscriptions.SubscriptionOrders;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test.Subscriptions;

/// <summary>
/// Solo se cambia hacia una licencia mayor (decisión del usuario, 2026-11-02, pendings/738): todos los módulos actuales,
/// límites iguales o mayores y algo más.
/// </summary>
public class LicenseUpgradeTest
{
    private static readonly ValueObjects.License Current = Monthly();

    [Fact]
    public void Compare_MoreModulesAndTheSameLimits_IsAnUpgradeThatGainsTheModule()
    {
        // Arrange
        var target = Catalog(modules: [Module(Organization), Module(Parking)]);

        // Act
        var result = LicenseUpgrade.Compare(Current, target);

        // Assert
        Assert.Equal((true, Parking), (result.IsUpgrade, result.GainedModules.Single().Id));
    }

    [Fact]
    public void Compare_MissingACurrentModule_IsNotAnUpgrade()
    {
        // Arrange
        var target = Catalog(modules: [Module(Parking)], attributes: new() { ["meetings.perYear"] = "50", ["meetings.recording"] = "true" });

        // Act
        var result = LicenseUpgrade.Compare(Current, target);

        // Assert
        Assert.False(result.IsUpgrade);
    }

    [Fact]
    public void Compare_ALowerNumericLimit_IsNotAnUpgrade()
    {
        // Arrange
        var target = Catalog(modules: [Module(Organization), Module(Parking)], attributes: new() { ["meetings.perYear"] = "6", ["meetings.recording"] = "false" });

        // Act
        var result = LicenseUpgrade.Compare(Current, target);

        // Assert
        Assert.False(result.IsUpgrade);
    }

    /// <summary>
    /// Encender la grabación es mejorar, y el texto descriptivo («Soporte») no cuenta ni para bien ni para mal.
    /// </summary>
    [Fact]
    public void Compare_TurningAFlagOnWithADifferentText_IsAnUpgradeThatGainsTheFlag()
    {
        // Arrange
        var target = Catalog(modules: [Module(Organization)], attributes: new() { ["meetings.perYear"] = "12", ["meetings.recording"] = "true", ["Soporte"] = "Teléfono" });

        // Act
        var result = LicenseUpgrade.Compare(Current, target);

        // Assert
        Assert.Equal((true, "true"), (result.IsUpgrade, result.GainedAttributes.Single().Value));
    }

    [Fact]
    public void Compare_TheSameLimitsAndModules_IsNotAnUpgrade()
    {
        // Arrange
        var target = Catalog(modules: [Module(Organization)], attributes: new() { ["meetings.perYear"] = "12", ["meetings.recording"] = "false", ["Soporte"] = "Prioritario" });

        // Act
        var result = LicenseUpgrade.Compare(Current, target);

        // Assert
        Assert.False(result.IsUpgrade);
    }

    [Fact]
    public void Compare_TheSameLicense_IsNotAnUpgrade()
    {
        // Arrange
        var target = Catalog(id: Current.Id, modules: [Module(Organization), Module(Parking)]);

        // Act
        var result = LicenseUpgrade.Compare(Current, target);

        // Assert
        Assert.False(result.IsUpgrade);
    }

    private static ModuleEntity Module(Guid id) => new() { Id = id, Name = id.ToString(), Description = "Module" };

    internal static LicenseAggregate Catalog(Guid? id = null, List<ModuleEntity>? modules = null, Dictionary<string, string>? attributes = null, long subTotal = 30_000_000) => LicenseAggregate.Create(
        id ?? Guid.NewGuid(), "Estándar", "Plan", "Plan", modules ?? [],
        [Price.Create(BillingType.Monthly, Money.FromLong(subTotal, "COP"), BillingModel.FlatRate, 0, 0)],
        Icon.Create("solar:home-2-linear", "#2563eb"), "Terms",
        attributes ?? new Dictionary<string, string> { ["meetings.perYear"] = "12", ["meetings.recording"] = "false" },
        isActive: true, isPopular: false, showInLandingPage: true, Guid.NewGuid());
}
