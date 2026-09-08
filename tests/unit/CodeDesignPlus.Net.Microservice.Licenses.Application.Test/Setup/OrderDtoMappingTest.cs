using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Setup;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Location;
using CodeDesignPlus.Net.ValueObjects.Payment;
using CodeDesignPlus.Net.ValueObjects.User;
// `License` y `Tenant` chocan a la vez con los tipos del SDK de Security y con los namespaces de la capa
// Application, asi que los alias los nombran por lo que son: snapshots del dominio de licencias.
using LicenseSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.License;
using LicenseModule = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.LicenseModule;
using TenantSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.Tenant;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Setup;

/// <summary>
/// Cubre que el DTO de la orden lleve lo que la pantalla de compra necesita para recuperarse.
/// </summary>
/// <remarks>
/// El mapeo de <c>OrderAggregate</c> a <c>OrderDto</c> es manual, y **omitia `ProvisioningStatus` y
/// `ProvisioningHistory`** aunque el DTO los declara. Como `GET Order/{id}` devuelve ese DTO,
/// `recoverOrderState()` en <c>/purchase/processing</c> leia un `provisioningStatus` que nunca llegaba: sus
/// cuatro ramas no entraban jamas y la pantalla dependia por completo de que el evento de SignalR llegase.
/// <para>
/// Es el mismo fallo silencioso de siempre: un campo que no viaja no rompe nada, solo deja de funcionar.
/// </para>
/// </remarks>
public class OrderDtoMappingTest
{
    private static readonly Guid OrderId = Guid.Parse("aa000000-0000-4000-8000-000000000001");
    private static readonly Guid PaymentId = Guid.Parse("bb000000-0000-4000-8000-000000000002");
    private static readonly Guid BuyerId = Guid.Parse("cc000000-0000-4000-8000-000000000003");
    private static readonly Guid TenantId = Guid.Parse("dd000000-0000-4000-8000-000000000004");
    private static readonly Guid UserId = Guid.Parse("ee000000-0000-4000-8000-000000000005");
    private static readonly Guid FileId = Guid.Parse("11000000-0000-4000-8000-000000000006");

    public OrderDtoMappingTest() => MapsterConfigLicense.Configure();

    [Fact]
    public void TheProvisioningStatusReachesTheDto()
    {
        // Es el campo del que depende `recoverOrderState()` para saber en que paso esta la compra.
        var order = BuildOrder();
        order.SetPaymentStatus(PaymentStatus.Succeeded, BuyerId);

        var dto = order.Adapt<OrderDto>();

        Assert.Equal(order.ProvisioningStatus, dto.ProvisioningStatus);
        Assert.NotEqual(ProvisioningStatus.None, dto.ProvisioningStatus);
    }

    [Fact]
    public void TheProvisioningHistoryReachesTheDto()
    {
        var order = BuildOrder();
        order.SetPaymentStatus(PaymentStatus.Succeeded, BuyerId);
        order.CompleteProvisioningStep("TenantProvisioning", UserId);

        var dto = order.Adapt<OrderDto>();

        Assert.NotEmpty(dto.ProvisioningHistory);
        Assert.Contains(dto.ProvisioningHistory, step => step.StepName == "TenantProvisioning");
    }

    [Fact]
    public void TheReceiptReferenceReachesTheDto()
    {
        var order = BuildOrder();
        order.AttachReceipt(FileId, $"PurchaseReceipt-{FileId}.pdf", $"licenses-pdf/{TenantId}", UserId);

        var dto = order.Adapt<OrderDto>();

        Assert.NotNull(dto.Receipt);
        Assert.Equal(FileId, dto.Receipt!.Id);
        Assert.Equal($"licenses-pdf/{TenantId}", dto.Receipt.Target);
    }

    [Fact]
    public void AnOrderWithoutReceiptMapsWithoutBlowingUp()
    {
        var dto = BuildOrder().Adapt<OrderDto>();

        Assert.Null(dto.Receipt);
    }

    [Fact]
    public void TheTenantDetailStillReaches()
    {
        // Guarda contra el mapeo manual: anadir un campo a mano es tambien la forma de perder otro.
        var dto = BuildOrder().Adapt<OrderDto>();

        Assert.Equal(OrderId, dto.Id);
        Assert.Equal(TenantId, dto.TenantDetail.Id);
        Assert.Equal(PaymentId, dto.PaymentId);
    }

    private static OrderAggregate BuildOrder() => OrderAggregate.Create(
        OrderId, PaymentId, BuildLicense(), BuildPaymentMethod(), BuildBuyer(), BuildTenant(), UserId);

    private static LicenseSnapshot BuildLicense()
    {
        var modules = new List<LicenseModule>
        {
            new(Guid.Parse("33000000-0000-4000-8000-000000000008"), "Administracion", "Cuotas y cargos"),
        };

        return LicenseSnapshot.Create(
            id: Guid.Parse("44000000-0000-4000-8000-000000000009"),
            name: "Esencial",
            total: Money.FromLong(178_500L, "COP"),
            tax: Money.FromLong(28_500L, "COP"),
            subTotal: Money.FromLong(150_000L, "COP"),
            billingType: BillingType.Monthly,
            billingModel: BillingModel.None,
            description: "Plan esencial para copropiedades pequenas",
            shortDescription: "Plan esencial",
            icon: Icon.Create("solar:home-2-linear", "#2563eb"),
            termsOfService: "https://kappali.com/terminos",
            isPopular: false,
            showInLandingPage: true,
            attributes: [],
            modules: modules);
    }

    // PaymentMethod exige que venga el detalle: uno de los dos medios, no ninguno.
    private static PaymentMethod BuildPaymentMethod() => PaymentMethod.Create(
        "CREDIT_CARD", null,
        CreditCard.Create("tok_test_0001", "4242", "2030/12", "Copropiedad Malpelo", "123", 1));

    private static Buyer BuildBuyer() =>
        Buyer.CreateMinimal(BuyerId, "Copropiedad Malpelo", "+573001234567", "compras@malpelo.com");

    private static TenantSnapshot BuildTenant() => TenantSnapshot.Create(
        TenantId, "Conjunto Malpelo", null,
        TypeDocument.Create("NIT", "Numero de Identificacion Tributaria"),
        "900123456", "+573001234567", "admin@malpelo.com", BuildLocation());

    private static Location BuildLocation()
    {
        var currency = Currency.Create(
            Guid.Parse("55000000-0000-4000-8000-00000000000a"), "Peso Colombiano", "COP", "$", 2, 170);

        var country = Country.Create(
            Guid.Parse("66000000-0000-4000-8000-00000000000b"), "Colombia", "CO", "COL", 170, "+57",
            "America/Bogota", currency);

        return Location.Create(
            country,
            State.Create(Guid.Parse("77000000-0000-4000-8000-00000000000c"), "Cundinamarca", "CUN"),
            City.Create(Guid.Parse("88000000-0000-4000-8000-00000000000d"), "Bogota", "America/Bogota"),
            Locality.Create(Guid.Parse("99000000-0000-4000-8000-00000000000e"), "Chapinero"),
            Neighborhood.Create(Guid.Parse("aa000000-0000-4000-8000-00000000000f"), "El Nogal"),
            "Calle 79 # 7-20",
            "110221");
    }
}
