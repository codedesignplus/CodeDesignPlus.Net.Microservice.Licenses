using CodeDesignPlus.Net.Microservice.Licenses.Domain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Location;
using CodeDesignPlus.Net.ValueObjects.Payment;
using CodeDesignPlus.Net.ValueObjects.User;
using LicenseSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.License;
using LicenseModule = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.LicenseModule;
using TenantSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.Tenant;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;

/// <summary>
/// Builds a valid order in its initial state (payment initiated, provisioning waiting for the payment).
/// </summary>
internal static class TestOrders
{
    public static readonly Guid OrderId = Guid.Parse("aa000000-0000-4000-8000-000000000101");
    public static readonly Guid PaymentId = Guid.Parse("bb000000-0000-4000-8000-000000000102");
    public static readonly Guid BuyerId = Guid.Parse("cc000000-0000-4000-8000-000000000103");
    public static readonly Guid TenantId = Guid.Parse("dd000000-0000-4000-8000-000000000104");

    public static OrderAggregate Build() => OrderAggregate.Create(
        OrderId, PaymentId, BuildLicense(), BuildPaymentMethod(), BuildBuyer(), BuildTenant(), BuyerId);

    /// <summary>An order whose payment was already approved: provisioning in progress, both steps pending.</summary>
    public static OrderAggregate Paid()
    {
        var order = Build();
        order.SetPaymentStatus(PaymentStatus.Succeeded, BuyerId);
        order.GetAndClearEvents();
        return order;
    }

    private static LicenseSnapshot BuildLicense() => LicenseSnapshot.Create(
        id: Guid.Parse("44000000-0000-4000-8000-000000000109"),
        name: "Esencial",
        total: Money.FromLong(14_280_000L, "COP"),
        tax: Money.FromLong(2_280_000L, "COP"),
        subTotal: Money.FromLong(12_000_000L, "COP"),
        billingType: BillingType.Monthly,
        billingModel: BillingModel.None,
        description: "Plan esencial",
        shortDescription: "Plan esencial",
        icon: Icon.Create("solar:verified-check-bold", "#3B82F6"),
        termsOfService: "Terms",
        isPopular: false,
        showInLandingPage: true,
        attributes: [],
        modules: [new LicenseModule(Guid.Parse("33000000-0000-4000-8000-000000000108"), "Organización", "Identidad")]);

    private static PaymentMethod BuildPaymentMethod() => PaymentMethod.Create(
        "VISA", null, CreditCard.Create("tok_test_0001", "0004", "2027/03", "APPROVED", "777", 1));

    private static Buyer BuildBuyer() =>
        Buyer.CreateMinimal(BuyerId, "Buyer", "+573001234567", "buyer@example.com");

    private static TenantSnapshot BuildTenant() => TenantSnapshot.Create(
        TenantId, "Conjunto Prueba", null,
        TypeDocument.Create("NIT", "Número de Identificación Tributaria"),
        "900123456", "+573001234567", "admin@example.com", BuildLocation());

    private static Location BuildLocation()
    {
        var currency = Currency.Create(Guid.Parse("55000000-0000-4000-8000-00000000010a"), "Peso Colombiano", "COP", "$", 2, 170);
        var country = Country.Create(
            Guid.Parse("66000000-0000-4000-8000-00000000010b"), "Colombia", "CO", "COL", 170, "+57", "America/Bogota", currency);

        return Location.Create(
            country,
            State.Create(Guid.Parse("77000000-0000-4000-8000-00000000010c"), "Bogotá, D.C.", "11"),
            City.Create(Guid.Parse("88000000-0000-4000-8000-00000000010d"), "Bogotá, D.C.", "America/Bogota"),
            Locality.Create(Guid.Parse("99000000-0000-4000-8000-00000000010e"), "Chapinero"),
            Neighborhood.Create(Guid.Parse("aa000000-0000-4000-8000-00000000010f"), "El Paraíso"),
            "Carrera 7 # 71-21",
            "110111");
    }
}
