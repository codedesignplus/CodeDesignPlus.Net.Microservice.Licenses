using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.User;
using License = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.License;
using LicenseModule = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.LicenseModule;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test.Subscriptions;

/// <summary>
/// Órdenes pagadas con su periodo, para armar suscripciones en las pruebas (pendings/735, 738).
/// </summary>
internal static class SubscriptionOrders
{
    public static readonly Guid Organization = Guid.Parse("33000000-0000-4000-8000-000000000008");
    public static readonly Guid Parking = Guid.Parse("33000000-0000-4000-8000-000000000018");

    /// <summary>
    /// A las 00:00 de Bogotá (UTC-5) de ese día.
    /// </summary>
    public static Instant Bogota(int year, int month, int day, int hour = 0) => Instant.FromUtc(year, month, day, hour, 0).Plus(Duration.FromHours(5));

    /// <summary>
    /// Una compra pagada cuyo periodo empieza en <paramref name="start"/>.
    /// </summary>
    public static OrderAggregate Paid(Instant start, License? license = null, Guid? id = null)
    {
        var order = OrderAggregate.Create(id ?? Guid.NewGuid(), Guid.NewGuid(), license ?? Monthly(), OrdenDePrueba.Comprador(), OrdenDePrueba.Copropiedad(), OrdenDePrueba.UserId);

        order.SetPaymentStatus(PaymentStatus.Succeeded, OrdenDePrueba.BuyerId, start);
        order.GetAndClearEvents();

        return order;
    }

    /// <summary>
    /// Una renovación pagada cuyo periodo empieza en <paramref name="start"/>.
    /// </summary>
    public static OrderAggregate PaidRenewal(Instant start, License? license = null)
    {
        var snapshot = license ?? Monthly();
        var charge = SubscriptionCharge.Compute(snapshot.Total, Money.Zero("COP"), Money.Zero("COP"));
        var order = OrderAggregate.CreateRenewal(Guid.NewGuid(), Guid.NewGuid(), snapshot, OrdenDePrueba.Comprador(), OrdenDePrueba.Copropiedad(), charge, OrdenDePrueba.UserId);

        order.SetPaymentStatus(PaymentStatus.Succeeded, OrdenDePrueba.BuyerId, start);
        order.GetAndClearEvents();

        return order;
    }

    /// <summary>
    /// Esencial mensual: base 150.000 (en centavos), IVA 28.500, un módulo, una cuota numérica, un sí/no y un texto.
    /// </summary>
    public static License Monthly(long subTotal = 15_000_000, long tax = 2_850_000, Dictionary<string, string>? attributes = null, List<LicenseModule>? modules = null, Guid? id = null) => License.Create(
        id: id ?? Guid.Parse("44000000-0000-4000-8000-000000000009"),
        name: "Esencial",
        total: Money.FromLong(subTotal + tax, "COP"),
        tax: Money.FromLong(tax, "COP"),
        subTotal: Money.FromLong(subTotal, "COP"),
        billingType: BillingType.Monthly,
        billingModel: BillingModel.FlatRate,
        description: "Plan esencial",
        shortDescription: "Plan esencial",
        icon: Icon.Create("solar:home-2-linear", "#2563eb"),
        termsOfService: "Terms",
        isPopular: false,
        showInLandingPage: true,
        attributes: attributes ?? new Dictionary<string, string> { ["meetings.perYear"] = "12", ["meetings.recording"] = "false", ["Soporte"] = "Correo" },
        modules: modules ?? [new LicenseModule(Organization, "Organización", "Identidad")]);
}
