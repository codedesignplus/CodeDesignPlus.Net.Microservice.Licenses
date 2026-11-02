using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using static CodeDesignPlus.Net.Microservice.Licenses.Domain.Test.Subscriptions.SubscriptionOrders;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test.Subscriptions;

/// <summary>
/// El crédito por lo no prestado al cambiar de licencia (pendings/738): días reales del periodo guardado, base sin
/// impuestos y redondeo a favor del cliente.
/// </summary>
public class ProrationTest
{
    /// <summary>
    /// Un tercio de un centavo se le reconoce al cliente: el importe se redondea hacia arriba, no al más cercano.
    /// </summary>
    [Fact]
    public void Prorate_AFractionOfAMinorUnit_RoundsUpInFavorOfTheCustomer()
    {
        // Act
        var credit = Proration.Prorate(Money.FromLong(100, "COP"), 1, 3);

        // Assert
        Assert.Equal(34, credit.Amount);
    }

    /// <summary>
    /// Los días salen del periodo guardado: febrero tiene 28, no los 30 que suponía el cálculo viejo. A mitad de febrero
    /// (14 días por delante de 28) se acredita exactamente la mitad de la base.
    /// </summary>
    [Fact]
    public void Credit_HalfOfFebruaryLeft_CreditsHalfOfTheBaseUsingTheRealDaysOfThePeriod()
    {
        // Arrange
        var order = Paid(Bogota(2027, 2, 1, 10));
        var subscription = SubscriptionTimeline.From([order])!;

        // Act
        var credited = Proration.Credit(subscription, Bogota(2027, 2, 15, 9));

        // Assert
        Assert.Equal((14, 28, 7_500_000L), (credited[0].UnusedDays, credited[0].PeriodDays, credited[0].Amount.Amount));
    }

    /// <summary>
    /// La base es lo facturado sin impuestos: con el periodo entero por delante se acredita la base (150.000), no el total
    /// con IVA (178.500).
    /// </summary>
    [Fact]
    public void Credit_TheWholePeriodLeft_CreditsTheBaseWithoutTaxes()
    {
        // Arrange
        var start = Bogota(2027, 3, 1, 10);
        var subscription = SubscriptionTimeline.From([Paid(start)])!;

        // Act
        var credited = Proration.Credit(subscription, start.Plus(Duration.FromHours(1)));

        // Assert
        Assert.Equal(15_000_000L, credited.Single().Amount.Amount);
    }

    /// <summary>
    /// Una renovación ya pagada que todavía no empieza se acredita entera, además de lo que falta del periodo en curso.
    /// </summary>
    [Fact]
    public void Credit_APaidRenewalNotStartedYet_IsCreditedInFull()
    {
        // Arrange
        var current = Paid(Bogota(2027, 3, 1, 10));
        var renewal = PaidRenewal(current.PeriodEnd!.Value);
        var subscription = SubscriptionTimeline.From([current, renewal])!;

        // Act
        var credited = Proration.Credit(subscription, Bogota(2027, 3, 21, 8));

        // Assert
        Assert.Equal((renewal.Id, 15_000_000L, renewal.PeriodStart!.Value), (credited[1].OrderId, credited[1].Amount.Amount, credited[1].From));
    }

    /// <summary>
    /// Un periodo vencido (la copropiedad en gracia) ya se prestó entero: no acredita nada.
    /// </summary>
    [Fact]
    public void Credit_AnExpiredPeriod_CreditsNothing()
    {
        // Arrange
        var subscription = SubscriptionTimeline.From([Paid(Bogota(2027, 1, 1, 10))])!;

        // Act
        var credited = Proration.Credit(subscription, Bogota(2027, 2, 5));

        // Assert
        Assert.Empty(credited);
    }

    /// <summary>
    /// El día del cambio cuenta como no prestado, porque el periodo nuevo empieza ese mismo día: cambiar el día en que
    /// empezó el periodo acredita la base entera.
    /// </summary>
    [Fact]
    public void Credit_ChangingTheSameDayThePeriodStarted_CountsTodayAsUnused()
    {
        // Arrange
        var subscription = SubscriptionTimeline.From([Paid(Bogota(2027, 4, 1, 10))])!;

        // Act
        var credited = Proration.Credit(subscription, Bogota(2027, 4, 1, 20));

        // Assert
        Assert.Equal((30, 30), (credited[0].UnusedDays, credited[0].PeriodDays));
    }
}
