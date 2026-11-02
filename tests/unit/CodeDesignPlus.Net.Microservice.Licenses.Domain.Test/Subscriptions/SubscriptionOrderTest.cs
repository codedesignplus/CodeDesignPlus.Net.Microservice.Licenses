using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using static CodeDesignPlus.Net.Microservice.Licenses.Domain.Test.Subscriptions.SubscriptionOrders;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test.Subscriptions;

/// <summary>
/// La orden de renovación y la de cambio de licencia (pendings/735, 738): su cobro, el periodo que fija el pago y el evento
/// que publica en vez de crear la copropiedad.
/// </summary>
public class SubscriptionOrderTest
{
    /// <summary>
    /// El crédito y el saldo a favor son un anticipo: primero el crédito, después el saldo, y lo que sobra del crédito queda
    /// como saldo a favor.
    /// </summary>
    [Fact]
    public void Compute_CreditGreaterThanThePrice_ChargesNothingAndKeepsTheRestAsCreditBalance()
    {
        // Act
        var charge = SubscriptionCharge.Compute(Money.FromLong(1_000, "COP"), Money.FromLong(1_200, "COP"), Money.FromLong(300, "COP"));

        // Assert
        Assert.Equal((1_000L, 0L, 0L, 500L), (charge.CreditApplied.Amount, charge.CreditBalanceApplied.Amount, charge.AmountDue.Amount, charge.RemainingCreditBalance.Amount));
    }

    [Fact]
    public void Compute_CreditSmallerThanThePrice_AppliesTheBalanceAfterTheCredit()
    {
        // Act
        var charge = SubscriptionCharge.Compute(Money.FromLong(1_000, "COP"), Money.FromLong(600, "COP"), Money.FromLong(300, "COP"));

        // Assert
        Assert.Equal((600L, 300L, 100L, 0L), (charge.CreditApplied.Amount, charge.CreditBalanceApplied.Amount, charge.AmountDue.Amount, charge.RemainingCreditBalance.Amount));
    }

    /// <summary>
    /// Una renovación pagada no crea la copropiedad: publica la renovación con el periodo que empieza donde terminaba lo
    /// pagado y dura un mes de calendario.
    /// </summary>
    [Fact]
    public void SetPaymentStatus_ARenewalPaid_PublishesTheRenewalWithThePeriodFromTheGivenStart()
    {
        // Arrange
        var license = Monthly();
        var order = OrderAggregate.CreateRenewal(Guid.NewGuid(), Guid.NewGuid(), license, OrdenDePrueba.Comprador(), OrdenDePrueba.Copropiedad(), SubscriptionCharge.Compute(license.Total, Money.Zero("COP"), Money.Zero("COP")), OrdenDePrueba.UserId);
        var start = Instant.FromUtc(2027, 3, 31, 15, 0);

        // Act
        order.SetPaymentStatus(PaymentStatus.Succeeded, OrdenDePrueba.BuyerId, start);

        // Assert
        var @event = Assert.IsType<SubscriptionRenewedDomainEvent>(Assert.Single(order.GetAndClearEvents()));
        Assert.Equal((start, Instant.FromUtc(2027, 4, 30, 15, 0), ProvisioningStatus.Completed), (@event.License.StartDate, @event.License.EndDate, order.ProvisioningStatus));
    }

    /// <summary>
    /// El cambio pagado publica la licencia nueva con lo que necesitan la nota crédito y la factura: el precio completo, el
    /// anticipo aplicado y lo cobrado.
    /// </summary>
    [Fact]
    public void SetPaymentStatus_AChangePaid_PublishesTheChangeWithTheFullPriceAndTheAdvance()
    {
        // Arrange
        var license = Monthly(subTotal: 30_000_000, tax: 0);
        var credited = new List<CreditedPeriod> { new(Guid.NewGuid(), Bogota(2027, 3, 16), Bogota(2027, 4, 1), 16, 31, Money.FromLong(15_000_000, "COP"), Money.FromLong(7_741_936, "COP")) };
        var charge = SubscriptionCharge.Compute(license.Total, Money.FromLong(7_741_936, "COP"), Money.Zero("COP"));
        var order = OrderAggregate.CreateChange(Guid.NewGuid(), Guid.NewGuid(), license, OrdenDePrueba.Comprador(), OrdenDePrueba.Copropiedad(), Guid.NewGuid(), credited, charge, OrdenDePrueba.UserId);

        // Act
        order.SetPaymentStatus(PaymentStatus.Succeeded, OrdenDePrueba.BuyerId);

        // Assert
        var @event = Assert.IsType<SubscriptionChangedDomainEvent>(Assert.Single(order.GetAndClearEvents()));
        Assert.Equal((30_000_000L, 7_741_936L, 22_258_064L), (@event.Billing.Total.Amount, @event.Billing.AdvanceApplied.Amount, @event.Billing.AmountPaid.Amount));
    }

    /// <summary>
    /// Una orden reemplazada antes de pagarse que llega a pagarse no cambia la licencia: no publica nada.
    /// </summary>
    [Fact]
    public void SetPaymentStatus_ASupersededOrderPaid_PublishesNothing()
    {
        // Arrange
        var license = Monthly();
        var order = OrderAggregate.CreateRenewal(Guid.NewGuid(), Guid.NewGuid(), license, OrdenDePrueba.Comprador(), OrdenDePrueba.Copropiedad(), SubscriptionCharge.Compute(license.Total, Money.Zero("COP"), Money.Zero("COP")), OrdenDePrueba.UserId);
        order.Supersede(Guid.NewGuid(), OrdenDePrueba.UserId);

        // Act
        order.SetPaymentStatus(PaymentStatus.Succeeded, OrdenDePrueba.BuyerId);

        // Assert
        Assert.Empty(order.GetAndClearEvents());
    }

    /// <summary>
    /// La compra sigue creando la copropiedad, ahora con el periodo que se guarda en la orden y no uno calculado aparte.
    /// </summary>
    [Fact]
    public void SetPaymentStatus_APurchasePaid_ProvisionsWithTheStoredPeriod()
    {
        // Arrange
        var order = OrdenDePrueba.Nueva();

        // Act
        order.SetPaymentStatus(PaymentStatus.Succeeded, OrdenDePrueba.BuyerId);

        // Assert
        var @event = Assert.IsType<OrderPaidAndReadyForProvisioningDomainEvent>(Assert.Single(order.GetAndClearEvents()));
        Assert.Equal((order.PeriodStart!.Value, order.PeriodEnd!.Value), (@event.License.StartDate, @event.License.EndDate));
    }

    /// <summary>
    /// Una orden guardada antes de existir los campos de la suscripción se sigue leyendo (regla 31): es una compra y no
    /// tiene periodo.
    /// </summary>
    [Fact]
    public void Deserialize_AnOrderStoredBeforeTheSubscriptionFields_ReadsAsAPurchaseWithoutPeriod()
    {
        // Arrange
        CodeDesignPlus.Net.Mongo.Extensions.MongoSerializerRegistration.RegisterSerializers();

        var order = Paid(Bogota(2027, 3, 1, 10));
        var document = order.ToBsonDocument();

        foreach (var field in new[] { "Type", "PeriodStart", "PeriodEnd", "CreditedPeriods", "PreviousLicenseId", "CreditBalanceApplied", "AmountDue", "SupersededBy" })
        {
            document.Remove(field);
        }

        // Act
        var stored = BsonSerializer.Deserialize<OrderAggregate>(document);

        // Assert
        Assert.Equal((OrderType.Purchase, (Instant?)null, stored.License.Total), (stored.Type, stored.PeriodStart, stored.AmountToCharge));
    }
}
