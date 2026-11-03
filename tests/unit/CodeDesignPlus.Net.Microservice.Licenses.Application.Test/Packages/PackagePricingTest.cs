using System.Collections.Generic;
using System.Linq;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Packages;

/// <summary>
/// El precio de un paquete lo calcula ms-licenses con lo que elige el cliente (pendings/739, regla 06).
/// </summary>
public class PackagePricingTest
{
    /// <summary>
    /// Virtual Básico cuesta su precio anual más el IVA del paquete: $1.990.000 + 19 % = $2.368.100.
    /// </summary>
    [Fact]
    public void Quote_AFixedPackage_ChargesItsPriceWithItsTax()
    {
        // Act
        var quote = PackagePricing.Quote(PackageFixtures.Basic(), null);

        // Assert
        Assert.Equal((199_000_000L, 37_810_000L, 236_810_000L, 4), (quote.SubTotal.Amount, quote.Tax.Amount, quote.Total.Amount, quote.Attributes.Count));
    }

    /// <summary>
    /// Dos asambleas hasta 200 conectados con su grabación y doce reuniones de consejo con grabación: los tramos y la
    /// grabación se cobran por reunión. 2 × (880.000 + 15.000) + 12 × (45.000 + 10.000) = 2.450.000.
    /// </summary>
    [Fact]
    public void Quote_AConfigurablePackage_PricesTiersAndTogglesPerMeeting()
    {
        // Arrange
        var selections = new Dictionary<string, string>
        {
            ["assemblies_number_meetings"] = "2",
            ["assemblies_max_participants"] = "200",
            ["council_number_meetings"] = "12",
            ["council_enable_record"] = "true",
        };

        // Act
        var quote = PackagePricing.Quote(PackageFixtures.Custom(), selections);

        // Assert
        Assert.Equal(245_000_000L, quote.SubTotal.Amount);
    }

    /// <summary>
    /// La cantidad mayor que cero enciende su sí/no (<c>assemblies_virtual_enable</c>); una cantidad en cero lo deja apagado.
    /// </summary>
    [Fact]
    public void Quote_AQuantityWithAnEnableKey_TurnsTheFlagOnOnlyAboveZero()
    {
        // Act
        var quote = PackagePricing.Quote(PackageFixtures.Custom(), new Dictionary<string, string> { ["assemblies_number_meetings"] = "1" });
        var attributes = quote.Attributes.ToDictionary(x => x.Key, x => x.Value);

        // Assert
        Assert.Equal(("true", "false"), (attributes["assemblies_virtual_enable"], attributes["council_virtual_enable"]));
    }

    /// <summary>
    /// La grabación de la asamblea va siempre incluida: pedirla apagada es un error, no un descuento.
    /// </summary>
    [Fact]
    public void Quote_TurningOffARequiredToggle_IsRejected()
    {
        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackagePricing.Quote(PackageFixtures.Custom(), new Dictionary<string, string> { ["assemblies_enable_record"] = "false" }));

        // Assert
        Assert.Equal("256", exception.Code);
    }

    /// <summary>
    /// Una cantidad fuera de su rango o un tramo que no existe no se cotizan.
    /// </summary>
    [Theory]
    [InlineData("assemblies_number_meetings", "7")]
    [InlineData("assemblies_max_participants", "150")]
    [InlineData("council_enable_record", "quizá")]
    public void Quote_AValueThePackageDoesNotAllow_IsRejected(string key, string value)
    {
        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackagePricing.Quote(PackageFixtures.Custom(), new Dictionary<string, string> { [key] = value }));

        // Assert
        Assert.Equal("255", exception.Code);
    }

    /// <summary>
    /// Una clave que no es un componente del paquete no se ignora en silencio.
    /// </summary>
    [Fact]
    public void Quote_AnUnknownSelection_IsRejected()
    {
        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackagePricing.Quote(PackageFixtures.Custom(), new Dictionary<string, string> { ["committee_number_meetings"] = "2" }));

        // Assert
        Assert.Equal("254", exception.Code);
    }

    /// <summary>
    /// Sin nada que cobrar no hay paquete.
    /// </summary>
    [Fact]
    public void Quote_NothingChosen_IsRejected()
    {
        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackagePricing.Quote(PackageFixtures.Custom(), new Dictionary<string, string> { ["assemblies_number_meetings"] = "0" }));

        // Assert
        Assert.Equal("257", exception.Code);
    }

    /// <summary>
    /// Si lo elegido suma menos que el mínimo del paquete, se cobra el mínimo.
    /// </summary>
    [Fact]
    public void Quote_BelowTheMinimum_ChargesTheMinimum()
    {
        // Act
        var quote = PackagePricing.Quote(PackageFixtures.Custom(minimumMinor: 100_000_000), null);

        // Assert
        Assert.Equal((true, 100_000_000L), (quote.MinimumApplied, quote.SubTotal.Amount));
    }

    /// <summary>
    /// Un tramo o un sí/no solo puede cobrarse por unidad de una cantidad del mismo paquete.
    /// </summary>
    [Fact]
    public void Create_APerUnitOfThatIsNotAQuantity_IsRejected()
    {
        // Arrange
        var definition = new PackageDefinition("Mal armado", "Corta", "Larga", Icon.Create("x", "#000000"), true, false, true, [], PackageKind.Configurable, "COP",
            PackageFixtures.Cop(0), PackageFixtures.Cop(0), TaxTreatment.Taxed, 1900, null, "Concepto", [],
            [new PackageComponent("record", PackageComponentKind.Toggle, AttributeCombination.Or, 0, 0, 0, 0, PackageFixtures.Cop(100), "missing", null, false, [])]);

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackageAggregate.Create(Guid.NewGuid(), definition, Guid.NewGuid()));

        // Assert
        Assert.Equal("252", exception.Code);
    }

    /// <summary>
    /// Un atributo de suma con un valor que no es un entero no se guarda: no se podría combinar.
    /// </summary>
    [Fact]
    public void Create_AnAttributeWhoseValueDoesNotMatchItsCombination_IsRejected()
    {
        // Arrange
        var definition = new PackageDefinition("Mal armado", "Corta", "Larga", Icon.Create("x", "#000000"), true, false, true, [], PackageKind.Fixed, "COP",
            PackageFixtures.Cop(100), PackageFixtures.Cop(0), TaxTreatment.Taxed, 1900, null, "Concepto",
            [new PackageAttribute("assemblies_number_meetings", "tres", AttributeCombination.Sum)], []);

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => PackageAggregate.Create(Guid.NewGuid(), definition, Guid.NewGuid()));

        // Assert
        Assert.Equal("249", exception.Code);
    }
}
