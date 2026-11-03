using System.Collections.Generic;
using System.Linq;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using LicenseSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.License;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Packages;

/// <summary>
/// Paquetes como los de la siembra de Colombia (pendings/739): Virtual Básico fijo y la sala a la medida configurable.
/// </summary>
internal static class PackageFixtures
{
    public static readonly Guid BasicId = Guid.Parse("7390a001-0000-4000-8000-000000000001");
    public static readonly Guid CustomId = Guid.Parse("7390a001-0000-4000-8000-000000000003");

    public static Money Cop(long minor) => Money.FromLong(minor, "COP");

    public static PackageAggregate Basic(long priceMinor = 199_000_000) => PackageAggregate.Create(BasicId, new PackageDefinition(
        "Virtual Básico", "Sala virtual", "Sala virtual de reuniones", Icon.Create("videocamera-record", "#2563eb"), true, false, true, [], PackageKind.Fixed, "COP",
        Cop(priceMinor), Cop(0), TaxTreatment.Taxed, 1900, null, "Sala virtual, paquete Virtual Básico",
        [
            new PackageAttribute("assemblies_virtual_enable", "true", AttributeCombination.Or),
            new PackageAttribute("assemblies_number_meetings", "3", AttributeCombination.Sum),
            new PackageAttribute("assemblies_max_participants", "100", AttributeCombination.Max),
            new PackageAttribute("assemblies_enable_record", "true", AttributeCombination.Or),
        ],
        []), Guid.NewGuid());

    public static PackageAggregate Custom(long minimumMinor = 0) => PackageAggregate.Create(CustomId, new PackageDefinition(
        "Sala virtual a la medida", "Sala a la medida", "Sala virtual armada por la copropiedad", Icon.Create("videocamera-record", "#ff522d"), true, false, true, [], PackageKind.Configurable, "COP",
        Cop(0), Cop(minimumMinor), TaxTreatment.Taxed, 1900, null, "Sala virtual, paquete a la medida",
        [],
        [
            new PackageComponent("assemblies_number_meetings", PackageComponentKind.Quantity, AttributeCombination.Sum, 0, 6, 1, 1, Cop(0), null, "assemblies_virtual_enable", false, []),
            new PackageComponent("assemblies_max_participants", PackageComponentKind.Tiered, AttributeCombination.Max, 0, 0, 0, 100, Cop(0), "assemblies_number_meetings", null, false, [new PackageTier(100, Cop(45_000_000)), new PackageTier(200, Cop(88_000_000)), new PackageTier(350, Cop(150_000_000))]),
            new PackageComponent("assemblies_enable_record", PackageComponentKind.Toggle, AttributeCombination.Or, 0, 0, 0, 1, Cop(1_500_000), "assemblies_number_meetings", null, true, []),
            new PackageComponent("council_number_meetings", PackageComponentKind.Quantity, AttributeCombination.Sum, 0, 24, 1, 0, Cop(0), null, "council_virtual_enable", false, []),
            new PackageComponent("council_max_participants", PackageComponentKind.Tiered, AttributeCombination.Max, 0, 0, 0, 15, Cop(0), "council_number_meetings", null, false, [new PackageTier(15, Cop(4_500_000))]),
            new PackageComponent("council_enable_record", PackageComponentKind.Toggle, AttributeCombination.Or, 0, 0, 0, 0, Cop(1_000_000), "council_number_meetings", null, false, []),
        ]), Guid.NewGuid());

    /// <summary>
    /// La copia de una licencia anual, con o sin atributos de reuniones.
    /// </summary>
    public static LicenseSnapshot AnnualLicense(Dictionary<string, string>? attributes = null) => LicenseSnapshot.Create(
        Guid.Parse("44000000-0000-4000-8000-000000000109"),
        "Estándar",
        Cop(345_100_000),
        Cop(55_100_000),
        Cop(290_000_000),
        BillingType.Annually,
        BillingModel.None,
        "Plan estándar",
        "Plan estándar",
        Icon.Create("solar:verified-check-bold", "#3B82F6"),
        "Terms",
        false,
        true,
        attributes ?? [],
        [new LicenseModule(Guid.Parse("33000000-0000-4000-8000-000000000108"), "Organización", "Identidad")]);

    /// <summary>
    /// Una compra pagada en <paramref name="start"/> con la licencia y los paquetes dados.
    /// </summary>
    public static OrderAggregate PaidPurchase(Instant start, LicenseSnapshot license, params PackageAggregate[] packages)
    {
        var lines = packages.Select(x => PackagePricing.Quote(x, null).ToLine(Guid.NewGuid(), null)).ToList();
        var order = OrderAggregate.Create(Guid.NewGuid(), Guid.NewGuid(), license, Buyer(), TestOrders.BuildTenant(), TestOrders.BuyerId, lines);

        order.SetPaymentStatus(PaymentStatus.Succeeded, TestOrders.BuyerId, start);
        order.GetAndClearEvents();

        return order;
    }

    private static CodeDesignPlus.Net.ValueObjects.User.Buyer Buyer() => CodeDesignPlus.Net.ValueObjects.User.Buyer.CreateMinimal(TestOrders.BuyerId, "Buyer", "+573001234567", "buyer@example.com");
}
