using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;

/// <summary>
/// Lo común de las pruebas de renovación y cambio de licencia (pendings/735, 738).
/// </summary>
internal static class SubscriptionFixtures
{
    public static readonly Guid Organization = Guid.Parse("33000000-0000-4000-8000-000000000108");
    public static readonly Guid Parking = Guid.Parse("33000000-0000-4000-8000-000000000118");

    public static Instant Now => SystemClock.Instance.GetCurrentInstant();

    /// <summary>
    /// Una licencia del catálogo con un precio mensual sin descuento ni IVA.
    /// </summary>
    public static LicenseAggregate Catalog(Guid id, long subTotal, params Guid[] modules) => LicenseAggregate.Create(
        id, "Estándar", "Plan", "Plan",
        [.. modules.Select(x => new ModuleEntity { Id = x, Name = x == Parking ? "Parqueaderos" : "Organización", Description = "Módulo" })],
        [Price.Create(BillingType.Monthly, Money.FromLong(subTotal, "COP"), BillingModel.FlatRate, 0, TaxTreatment.Excluded, 0, "Test legal basis")],
        Icon.Create("solar:home-2-linear", "#2563eb"), "Terms", [], isActive: true, isPopular: false, showInLandingPage: true, "Invoice concept", [], Guid.NewGuid());

    /// <summary>
    /// Hace que el repositorio del saldo aplique el cambio sobre <paramref name="balance"/>.
    /// </summary>
    public static void AppliesTo(this Mock<ICreditBalanceRepository> repository, CreditBalanceAggregate balance)
    {
        repository
            .Setup(x => x.UpdateWithRetryAsync(It.IsAny<Func<Task<CreditBalanceAggregate>>>(), It.IsAny<Func<CreditBalanceAggregate, bool>>(), It.IsAny<CancellationToken>()))
            .Callback<Func<Task<CreditBalanceAggregate>>, Func<CreditBalanceAggregate, bool>, CancellationToken>((_, mutate, _) => mutate(balance))
            .ReturnsAsync(true);
    }

    /// <summary>
    /// Una renovación esperando su pago, con saldo a favor apartado.
    /// </summary>
    public static OrderAggregate AwaitingRenewal(long creditBalanceApplied = 0)
    {
        var license = TestOrders.BuildLicense();
        var charge = SubscriptionCharge.Compute(license.Total, Money.Zero("COP"), Money.FromLong(creditBalanceApplied, "COP"));

        return OrderAggregate.CreateRenewal(Guid.NewGuid(), Guid.NewGuid(), license, Buyer(), TestOrders.BuildTenant(), charge, TestOrders.BuyerId);
    }

    public static CodeDesignPlus.Net.ValueObjects.User.Buyer Buyer() => CodeDesignPlus.Net.ValueObjects.User.Buyer.CreateMinimal(TestOrders.BuyerId, "Buyer", "+573001234567", "buyer@example.com");

    public static List<OrderAggregate> Paid(params OrderAggregate[] orders) => [.. orders];
}
