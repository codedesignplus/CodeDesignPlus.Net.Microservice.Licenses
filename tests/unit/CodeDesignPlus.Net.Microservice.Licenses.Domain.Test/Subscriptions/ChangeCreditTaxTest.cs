using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using static CodeDesignPlus.Net.Microservice.Licenses.Domain.Test.Subscriptions.SubscriptionOrders;
using License = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.License;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test.Subscriptions;

/// <summary>
/// El crédito por lo no prestado devuelve también el IVA que se pagó por ello, con la tarifa de la factura anterior, y el
/// pago del cambio dice la base y el IVA de lo que de verdad se cobra (pendings/738, validación del 2026-11-02 en
/// «Conjunto Prueba Licencias»: Estándar a 345.100 con IVA, cambio a Élite a 773.500).
/// </summary>
public class ChangeCreditTaxTest
{
    /// <summary>
    /// Estándar mensual: base 290.000 e IVA del 19 % (55.100), en centavos.
    /// </summary>
    private static License Standard() => Taxed(29_000_000, 1900);

    /// <summary>
    /// Élite mensual: base 650.000 e IVA del 19 % (123.500), en centavos.
    /// </summary>
    private static License Elite() => Taxed(65_000_000, 1900);

    private static License Taxed(long subTotal, int basisPoints)
    {
        var price = Price.Create(BillingType.Monthly, Money.FromLong(subTotal, "COP"), BillingModel.FlatRate, 0, TaxTreatment.Taxed, basisPoints, null);

        return Monthly(subTotal: price.SubTotal.Amount, tax: price.Tax.Amount).WithBillingTerms(price, "Licencia");
    }

    /// <summary>
    /// Con los 30 días por delante se acredita lo pagado entero: la base (290.000) y su IVA (55.100), 345.100. Antes se
    /// acreditaba solo la base y la copropiedad perdía el IVA del periodo que no se le prestó.
    /// </summary>
    [Fact]
    public void Credit_TheWholePeriodLeftOfATaxedOrder_CreditsTheBaseAndItsTax()
    {
        // Arrange
        var start = Bogota(2027, 3, 1, 10);
        var subscription = SubscriptionTimeline.From([Paid(start, Standard())])!;

        // Act
        var credited = Proration.Credit(subscription, start.Plus(Duration.FromHours(1))).Single();

        // Assert
        Assert.Equal((29_000_000L, 5_510_000L, 1900, 34_510_000L), (credited.Amount.Amount, credited.Tax!.Amount, credited.TaxBasisPoints, credited.Total.Amount));
    }

    /// <summary>
    /// Con 24 de 30 días por delante: base 232.000 y su IVA al 19 % de la factura anterior, 44.080.
    /// </summary>
    [Fact]
    public void Credit_PartOfATaxedPeriodLeft_CreditsTheTaxOfTheCreditedBaseWithTheRateOfThatInvoice()
    {
        // Arrange
        var subscription = SubscriptionTimeline.From([Paid(Bogota(2027, 4, 1, 10), Standard())])!;

        // Act
        var credited = Proration.Credit(subscription, Bogota(2027, 4, 7, 9)).Single();

        // Assert
        Assert.Equal((24, 23_200_000L, 4_408_000L), (credited.UnusedDays, credited.Amount.Amount, credited.Tax!.Amount));
    }

    /// <summary>
    /// El IVA de la base acreditada se redondea hacia arriba, a favor del cliente: el 19 % de un centavo es un centavo.
    /// </summary>
    [Fact]
    public void ProrateTax_AFractionOfAMinorUnit_RoundsUpInFavorOfTheCustomer()
    {
        // Act
        var tax = Proration.ProrateTax(Standard(), Money.FromLong(1, "COP"), 1, 30);

        // Assert
        Assert.Equal(1, tax.Amount);
    }

    /// <summary>
    /// Por redondear hacia arriba no se acredita más IVA del que llevó la factura.
    /// </summary>
    [Fact]
    public void ProrateTax_RoundingUp_NeverCreditsMoreThanTheInvoicedTax()
    {
        // Arrange
        var license = Monthly(subTotal: 10, tax: 1).WithBillingTerms(Price.Create(BillingType.Monthly, Money.FromLong(10, "COP"), BillingModel.FlatRate, 0, TaxTreatment.Taxed, 1900, null), "Licencia");

        // Act
        var tax = Proration.ProrateTax(license, Money.FromLong(10, "COP"), 1, 1);

        // Assert
        Assert.Equal(1, tax.Amount);
    }

    /// <summary>
    /// Una factura excluida de IVA no devuelve IVA.
    /// </summary>
    [Fact]
    public void Credit_AnOrderWithoutTax_CreditsNoTax()
    {
        // Arrange
        var start = Bogota(2027, 3, 1, 10);
        var subscription = SubscriptionTimeline.From([Paid(start, Monthly(tax: 0))])!;

        // Act
        var credited = Proration.Credit(subscription, start.Plus(Duration.FromHours(1))).Single();

        // Assert
        Assert.Equal((0L, 15_000_000L), (credited.Tax!.Amount, credited.Total.Amount));
    }

    /// <summary>
    /// El caso validado: Élite a 773.500 menos lo pagado por Estándar (345.100) deja 428.400 por pagar, y el pago dice base
    /// 360.000 (650.000 − 290.000) e IVA 68.400 (123.500 − 55.100). Antes se cobraban 483.500 y ms-payments recibía el neto
    /// partido como si llevara el IVA del precio dentro (406.302,53 + 77.197,47).
    /// </summary>
    [Fact]
    public void ChargeBreakdown_AChangeWithTheWholePeriodCredited_ChargesThePriceLessTheCreditWithItsTax()
    {
        // Arrange
        var start = Bogota(2027, 3, 1, 10);
        var credited = Proration.Credit(SubscriptionTimeline.From([Paid(start, Standard())])!, start.Plus(Duration.FromHours(1)));
        var order = Change(Elite(), credited, Money.Zero("COP"));

        // Act
        var (subTotal, tax) = order.ChargeBreakdown();

        // Assert
        Assert.Equal((42_840_000L, 36_000_000L, 6_840_000L), (order.AmountToCharge.Amount, subTotal.Amount, tax.Amount));
    }

    /// <summary>
    /// Si la factura anterior no llevó IVA (excluida), el crédito no devuelve IVA y lo cobrado lleva el IVA entero del
    /// precio: base 360.000 e IVA 123.500. Repartir el neto en la proporción del precio daba 406.302,53 + 77.197,47.
    /// </summary>
    [Fact]
    public void ChargeBreakdown_ACreditWithoutTax_ChargesTheWholeTaxOfThePrice()
    {
        // Arrange
        var start = Bogota(2027, 3, 1, 10);
        var credited = Proration.Credit(SubscriptionTimeline.From([Paid(start, Monthly(subTotal: 29_000_000, tax: 0))])!, start.Plus(Duration.FromHours(1)));
        var order = Change(Elite(), credited, Money.Zero("COP"));

        // Act
        var (subTotal, tax) = order.ChargeBreakdown();

        // Assert
        Assert.Equal((48_350_000L, 36_000_000L, 12_350_000L), (order.AmountToCharge.Amount, subTotal.Amount, tax.Amount));
    }

    /// <summary>
    /// Lo que paga el saldo a favor se reparte en la proporción de lo que queda: la base y el IVA del pago suman
    /// exactamente lo cobrado.
    /// </summary>
    [Fact]
    public void ChargeBreakdown_WithCreditBalanceApplied_AddsUpToWhatIsCharged()
    {
        // Arrange
        var start = Bogota(2027, 3, 1, 10);
        var credited = Proration.Credit(SubscriptionTimeline.From([Paid(start, Standard())])!, start.Plus(Duration.FromHours(1)));
        var order = Change(Elite(), credited, Money.FromLong(1_000_001, "COP"));

        // Act
        var (subTotal, tax) = order.ChargeBreakdown();

        // Assert
        Assert.Equal((41_839_999L, 41_839_999L, 6_680_335L), (order.AmountToCharge.Amount, subTotal.Amount + tax.Amount, tax.Amount));
    }

    /// <summary>
    /// Si lo no usado cubre el precio, no se cobra nada y el sobrante, con su IVA, queda como saldo a favor.
    /// </summary>
    [Fact]
    public void Compute_ACreditWithTaxGreaterThanThePrice_LeavesTheRestWithItsTaxAsCreditBalance()
    {
        // Arrange
        var start = Bogota(2027, 3, 1, 10);
        var credited = Proration.Credit(SubscriptionTimeline.From([Paid(start, Standard())])!, start.Plus(Duration.FromHours(1)));
        var target = Taxed(25_000_000, 1900);

        // Act
        var order = Change(target, credited, Money.Zero("COP"));
        var charge = SubscriptionCharge.Compute(target.Total, order.ProrationCredit, Money.Zero("COP"));

        // Assert
        Assert.Equal((0L, 34_510_000L - 29_750_000L, 0L, 0L), (charge.AmountDue.Amount, charge.RemainingCreditBalance.Amount, order.ChargeBreakdown().SubTotal.Amount, order.ChargeBreakdown().Tax.Amount));
    }

    /// <summary>
    /// Un cambio guardado antes de acreditar el IVA se sigue leyendo (regla 31): sin IVA acreditado, su total es la base
    /// que se acreditó entonces.
    /// </summary>
    [Fact]
    public void Deserialize_ACreditedPeriodStoredBeforeTheTax_ReadsTheBaseAsItsTotal()
    {
        // Arrange
        CodeDesignPlus.Net.Mongo.Extensions.MongoSerializerRegistration.RegisterSerializers();

        var start = Bogota(2027, 3, 1, 10);
        var credited = Proration.Credit(SubscriptionTimeline.From([Paid(start, Standard())])!, start.Plus(Duration.FromHours(1)));
        var document = Change(Elite(), credited, Money.Zero("COP")).ToBsonDocument();

        foreach (var period in document["CreditedPeriods"].AsBsonArray.Select(x => x.AsBsonDocument))
        {
            period.Remove("Tax");
            period.Remove("TaxBasisPoints");
        }

        document.Remove("FailureReason");

        // Act
        var stored = BsonSerializer.Deserialize<OrderAggregate>(document);

        // Assert
        Assert.Equal((29_000_000L, 0L, (PaymentFailureReason?)null), (stored.ProrationCredit.Amount, stored.ProrationCreditTax.Amount, stored.FailureReason));
    }

    /// <summary>
    /// Un pago rechazado deja en la orden por qué y lo que dijo el banco: antes la orden quedaba con el error nulo y el
    /// motivo solo vivía en ms-payments.
    /// </summary>
    [Fact]
    public void SetPaymentStatus_AFailedPayment_KeepsTheReasonAndTheProviderMessage()
    {
        // Arrange
        var order = Change(Elite(), [], Money.Zero("COP"));

        // Act
        order.SetPaymentStatus(PaymentStatus.Failed, OrdenDePrueba.BuyerId, failureReason: PaymentFailureReason.Declined, providerMessage: " La transacción fue rechazada (Sandbox) ");

        // Assert
        Assert.Equal((PaymentFailureReason.Declined, "La transacción fue rechazada (Sandbox)"), (order.FailureReason, order.Error));
    }

    /// <summary>
    /// Una sesión de pago vencida sin motivo publicado queda como vencida, no sin explicación.
    /// </summary>
    [Fact]
    public void SetPaymentStatus_AnExpiredPaymentWithoutReason_IsRecordedAsExpired()
    {
        // Arrange
        var order = Change(Elite(), [], Money.Zero("COP"));

        // Act
        order.SetPaymentStatus(PaymentStatus.Expired, OrdenDePrueba.BuyerId);

        // Assert
        Assert.Equal(PaymentFailureReason.Expired, order.FailureReason);
    }

    private static OrderAggregate Change(License target, List<CreditedPeriod> credited, Money creditBalance)
    {
        var credit = credited.Aggregate(Money.Zero("COP"), (sum, x) => sum + x.Total);
        var charge = SubscriptionCharge.Compute(target.Total, credit, creditBalance);

        return OrderAggregate.CreateChange(Guid.NewGuid(), Guid.NewGuid(), target, OrdenDePrueba.Comprador(), OrdenDePrueba.Copropiedad(), Guid.NewGuid(), credited, charge, OrdenDePrueba.UserId);
    }
}
