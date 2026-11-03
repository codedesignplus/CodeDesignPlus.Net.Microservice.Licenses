using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order.Services;

/// <summary>
/// Cómo se leen los importes, el periodo y el nombre del comprobante de licencias (pendings/738).
/// </summary>
public class ReceiptTextTest
{
    [Theory]
    [InlineData(77_350_000, "$ 773.500,00")]
    [InlineData(5, "$ 0,05")]
    [InlineData(0, "$ 0,00")]
    [InlineData(123_456_789_012, "$ 1.234.567.890,12")]
    [InlineData(-23_200_000, "-$ 232.000,00")]
    public void Amount_Cop_IsWrittenAsInTheScreens(long minorUnits, string expected)
    {
        // Arrange
        var amount = Money.FromLong(minorUnits, "COP");

        // Act
        var text = ReceiptText.Amount(amount, TestOrders.Cop);

        // Assert
        Assert.Equal(expected, text);
    }

    [Fact]
    public void Amount_CurrencyWithoutDecimals_UsesTheDecimalsOfTheCatalog()
    {
        // Arrange
        var clp = Currency.Create(Guid.NewGuid(), "Peso Chileno", "CLP", "$", 0, 152);

        // Act
        var text = ReceiptText.Amount(Money.FromLong(1_500_500, "CLP"), clp);

        // Assert
        Assert.Equal("$ 1.500.500", text);
    }

    [Fact]
    public void Period_UnpaidOrder_IsEmpty()
    {
        // Arrange
        var order = SubscriptionFixtures.AwaitingRenewal();

        // Act
        var period = ReceiptText.Period(order, DateTimeZoneProviders.Tzdb["America/Bogota"]);

        // Assert
        Assert.Equal(string.Empty, period);
    }

    [Fact]
    public void ReceiptFileName_Renewal_SaysItIsARenewal()
    {
        // Arrange
        var order = SubscriptionFixtures.AwaitingRenewal();

        // Act
        var name = ReceiptText.ReceiptFileName(order);

        // Assert
        Assert.Equal($"ComprobanteRenovacion-{order.Id}.pdf", name);
    }

    [Fact]
    public void ReceiptFileName_Purchase_SaysItIsAPurchase()
    {
        // Arrange
        var order = TestOrders.Build();

        // Act
        var name = ReceiptText.ReceiptFileName(order);

        // Assert
        Assert.Equal($"ComprobanteCompra-{TestOrders.OrderId}.pdf", name);
    }
}
