using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Location;
using CodeDesignPlus.Net.ValueObjects.Payment;
using CodeDesignPlus.Net.ValueObjects.User;
// El SDK de Security declara sus propios `License` y `Tenant`; aqui siempre se quiere el snapshot del
// dominio de licencias, asi que los alias resuelven la ambiguedad de una vez.
using License = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.License;
using LicenseModule = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.LicenseModule;
using Tenant = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.Tenant;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test;

/// <summary>
/// Cubre que la orden conserve la referencia del recibo de compra.
/// </summary>
/// <remarks>
/// El PDF del recibo ya se generaba y se subia a ms-filestorage, pero la referencia se perdia: solo
/// viajaba adjunta al correo, y la URL firmada se calculaba en una variable que nadie leia. La pantalla de
/// compra no tenia de donde sacarlo, asi que su boton "ver recibo" no aparecia nunca.
/// <para>
/// Se guarda la referencia y **no la URL firmada**: la firma caduca a los 7 dias, asi que una URL
/// persistida seria un enlace roto con fecha de caducidad. Quien lo muestre pide una firma nueva.
/// </para>
/// <para>
/// Se reutiliza <see cref="FileAttachment"/>, que es exactamente la misma referencia que el handler ya
/// construia para adjuntar el PDF al correo. Un value object nuevo para lo mismo habria sido un segundo
/// sitio donde equivocarse.
/// </para>
/// </remarks>
public class OrderReceiptTest
{
    private static readonly Guid OrderId = Guid.Parse("aa000000-0000-4000-8000-000000000001");
    private static readonly Guid PaymentId = Guid.Parse("bb000000-0000-4000-8000-000000000002");
    private static readonly Guid BuyerId = Guid.Parse("cc000000-0000-4000-8000-000000000003");
    private static readonly Guid TenantId = Guid.Parse("dd000000-0000-4000-8000-000000000004");
    private static readonly Guid UserId = Guid.Parse("ee000000-0000-4000-8000-000000000005");
    private static readonly Guid FileId = Guid.Parse("11000000-0000-4000-8000-000000000006");
    private static readonly Guid OtherFileId = Guid.Parse("22000000-0000-4000-8000-000000000007");

    private static readonly string FileName = $"PurchaseReceipt-{FileId}.pdf";
    private const string Target = "licenses-pdf/dd000000-0000-4000-8000-000000000004";

    [Fact]
    public void ANewOrderHasNoReceipt()
    {
        var order = BuildOrder();

        Assert.Null(order.Receipt);
    }

    [Fact]
    public void AttachingTheReceiptKeepsTheStorageReference()
    {
        var order = BuildOrder();

        order.AttachReceipt(FileId, FileName, Target, UserId);

        Assert.NotNull(order.Receipt);
        Assert.Equal(FileId, order.Receipt!.Id);
        Assert.Equal(FileName, order.Receipt.Name);
        Assert.Equal(Target, order.Receipt.Target);
    }

    [Fact]
    public void TheReceiptDoesNotKeepASignedUrl()
    {
        // La firma que devuelve ms-filestorage caduca a los 7 dias. Persistirla dejaria un enlace roto con
        // fecha, asi que el value object no tiene donde guardarla ni debe tenerlo.
        var order = BuildOrder();

        order.AttachReceipt(FileId, FileName, Target, UserId);

        Assert.DoesNotContain(
            order.Receipt!.GetType().GetProperties(),
            property => property.Name.Contains("Url", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void RegeneratingTheReceiptReplacesTheReference()
    {
        // El webhook de la pasarela reentrega y el recibo se vuelve a generar con otro fileId: la orden
        // tiene que apuntar al ultimo, no al primero, o el boton abriria un PDF huerfano.
        var order = BuildOrder();
        order.AttachReceipt(FileId, FileName, Target, UserId);

        order.AttachReceipt(OtherFileId, $"PurchaseReceipt-{OtherFileId}.pdf", Target, UserId);

        Assert.Equal(OtherFileId, order.Receipt!.Id);
    }

    [Fact]
    public void AttachingTheReceiptLeavesTheAuditTrail()
    {
        var order = BuildOrder();

        order.AttachReceipt(FileId, FileName, Target, UserId);

        Assert.Equal(UserId, order.UpdatedBy);
        Assert.NotNull(order.UpdatedAt);
    }

    private static OrderAggregate BuildOrder() => OrderAggregate.Create(
        OrderId, PaymentId, BuildLicense(), BuildPaymentMethod(), BuildBuyer(), BuildTenant(), UserId);

    private static License BuildLicense()
    {
        var total = Money.FromLong(178_500L, "COP");
        var tax = Money.FromLong(28_500L, "COP");
        var subTotal = Money.FromLong(150_000L, "COP");

        var modules = new List<LicenseModule>
        {
            new(Guid.Parse("33000000-0000-4000-8000-000000000008"), "Administracion", "Cuotas y cargos"),
        };

        return License.Create(
            id: Guid.Parse("44000000-0000-4000-8000-000000000009"),
            name: "Esencial",
            total: total,
            tax: tax,
            subTotal: subTotal,
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

    private static Tenant BuildTenant() => Tenant.Create(
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
