using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test;

/// <summary>
/// Las reglas de los precios de una licencia, iguales en la pantalla, la API y la ficha de ayuda (plan 063 de pendings/).
/// </summary>
public class LicensePricingRulesTest
{
    private static Price Monthly(string currency = "COP", BillingModel model = BillingModel.FlatRate)
        => Price.Create(BillingType.Monthly, Money.FromDecimal(120000, currency, 2), model, 0, 1900);

    private static Price Annually()
        => Price.Create(BillingType.Annually, Money.FromDecimal(1440000, "COP", 2), BillingModel.FlatRate, 1500, 1900);

    private static Icon AnyIcon() => Icon.Create("solar:verified-check-bold", "#3B82F6");

    private static LicenseAggregate CreateWith(List<Price> prices)
        => LicenseAggregate.Create(Guid.NewGuid(), "Esencial", "Short", "Description", [], prices,
            AnyIcon(), "Terms", [], true, false, true, Guid.NewGuid());

    /// <summary>Un mensual y un anual es la forma normal de una licencia.</summary>
    [Fact]
    public void Create_OneMonthlyAndOneAnnualPrice_IsAccepted()
    {
        var license = CreateWith([Monthly(), Annually()]);

        Assert.Equal(2, license.Prices.Count);
    }

    /// <summary>
    /// Dos precios mensuales se rechazan aunque cambien la moneda o el modelo: la compra elige el precio solo por la
    /// modalidad, y cobraría uno cualquiera de los dos.
    /// </summary>
    [Theory]
    [InlineData("USD", BillingModel.FlatRate)]
    [InlineData("COP", BillingModel.PerUser)]
    public void Create_TwoPricesWithTheSameBillingType_IsRejected(string currency, BillingModel model)
    {
        var exception = Assert.Throws<CodeDesignPlusException>(() => CreateWith([Monthly(), Monthly(currency, model)]));

        Assert.Equal(Errors.DuplicatePricingStrategyFound.GetCode(), exception.Code);
    }

    /// <summary>Editar aplica la misma regla que crear.</summary>
    [Fact]
    public void Update_TwoPricesWithTheSameBillingType_IsRejected()
    {
        var license = CreateWith([Monthly()]);

        var exception = Assert.Throws<CodeDesignPlusException>(() => license.Update(
            "Esencial", "Short", "Description", [], [Monthly(), Monthly("USD")],
            AnyIcon(), "Terms", [], true, false, true, Guid.NewGuid()));

        Assert.Equal(Errors.DuplicatePricingStrategyFound.GetCode(), exception.Code);
    }

    /// <summary>El 100 % es el tope del descuento; por la API se podía guardar un 150 %.</summary>
    [Fact]
    public void Price_DiscountOverOneHundredPercent_IsRejected()
    {
        var exception = Assert.Throws<CodeDesignPlusException>(() =>
            Price.Create(BillingType.Monthly, Money.FromDecimal(100, "COP", 2), BillingModel.FlatRate, Price.MaxBasisPoints + 1, 1900));

        Assert.Equal(Errors.DiscountLicenseCannotExceedOneHundredPercent.GetCode(), exception.Code);
    }

    /// <summary>El impuesto tampoco puede pasar del 100 %.</summary>
    [Fact]
    public void Price_TaxOverOneHundredPercent_IsRejected()
    {
        var exception = Assert.Throws<CodeDesignPlusException>(() =>
            Price.Create(BillingType.Monthly, Money.FromDecimal(100, "COP", 2), BillingModel.FlatRate, 0, Price.MaxBasisPoints + 1));

        Assert.Equal(Errors.TaxLicenseCannotExceedOneHundredPercent.GetCode(), exception.Code);
    }

    /// <summary>Exactamente el 100 % sigue siendo válido.</summary>
    [Fact]
    public void Price_ExactlyOneHundredPercent_IsAccepted()
    {
        var price = Price.Create(BillingType.Monthly, Money.FromDecimal(100, "COP", 2), BillingModel.FlatRate, Price.MaxBasisPoints, Price.MaxBasisPoints);

        Assert.Equal(Price.MaxBasisPoints, price.DiscountBasisPoints);
    }
}
