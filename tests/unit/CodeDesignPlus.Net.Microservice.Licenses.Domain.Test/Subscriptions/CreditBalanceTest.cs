using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test.Subscriptions;

/// <summary>
/// El saldo a favor de una copropiedad (pendings/738): cada movimiento se aplica una vez y no se gasta lo que no hay.
/// </summary>
public class CreditBalanceTest
{
    private static readonly Guid Tenant = Guid.Parse("dd000000-0000-4000-8000-000000000004");

    [Fact]
    public void Apply_TheSameMovementTwice_CountsItOnce()
    {
        // Arrange
        var balance = CreditBalanceAggregate.Create(Tenant, "COP");
        var movement = CreditMovement.ForOrder(Guid.NewGuid(), CreditMovementKind.CreditNote, Money.FromLong(500, "COP"), Instant.FromUtc(2027, 3, 1, 0, 0));

        // Act
        balance.Apply(movement);
        var again = balance.Apply(CreditMovement.ForOrder(movement.OrderId, CreditMovementKind.CreditNote, Money.FromLong(500, "COP"), Instant.FromUtc(2027, 3, 2, 0, 0)));

        // Assert
        Assert.Equal((false, 500L), (again, balance.Balance.Amount));
    }

    [Fact]
    public void Apply_ADebitGreaterThanTheBalance_IsRejected()
    {
        // Arrange
        var balance = CreditBalanceAggregate.Create(Tenant, "COP");
        balance.Apply(CreditMovement.ForOrder(Guid.NewGuid(), CreditMovementKind.CreditNote, Money.FromLong(500, "COP"), Instant.FromUtc(2027, 3, 1, 0, 0)));

        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => balance.Apply(CreditMovement.ForOrder(Guid.NewGuid(), CreditMovementKind.AppliedToOrder, Money.FromLong(501, "COP"), Instant.FromUtc(2027, 3, 1, 0, 0))));

        // Assert
        Assert.Equal(Errors.InsufficientCreditBalance.GetCode(), exception.Code);
    }

    [Fact]
    public void Apply_ADebitThenItsRelease_LeavesTheBalanceAsItWas()
    {
        // Arrange
        var order = Guid.NewGuid();
        var balance = CreditBalanceAggregate.Create(Tenant, "COP");
        balance.Apply(CreditMovement.ForOrder(Guid.NewGuid(), CreditMovementKind.CreditNote, Money.FromLong(500, "COP"), Instant.FromUtc(2027, 3, 1, 0, 0)));

        // Act
        balance.Apply(CreditMovement.ForOrder(order, CreditMovementKind.AppliedToOrder, Money.FromLong(300, "COP"), Instant.FromUtc(2027, 3, 1, 0, 0)));
        balance.Apply(CreditMovement.ForOrder(order, CreditMovementKind.Released, Money.FromLong(300, "COP"), Instant.FromUtc(2027, 3, 1, 0, 0)));

        // Assert
        Assert.Equal((500L, 3), (balance.Balance.Amount, balance.Movements.Count));
    }
}
