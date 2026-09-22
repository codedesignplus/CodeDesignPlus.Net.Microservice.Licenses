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
/// Arma un pedido valido para las pruebas del dominio.
/// </summary>
/// <remarks>
/// Vive aparte porque construirlo cuesta cincuenta lineas —licencia, medio de pago, comprador, copropiedad
/// y su ubicacion completa— y tenerlo dos veces seria tener dos sitios donde el fixture puede divergir del
/// dominio sin que nadie lo note.
/// </remarks>
public static class OrdenDePrueba
{
    public static readonly Guid OrderId = Guid.Parse("aa000000-0000-4000-8000-000000000001");
    public static readonly Guid PaymentId = Guid.Parse("bb000000-0000-4000-8000-000000000002");
    public static readonly Guid BuyerId = Guid.Parse("cc000000-0000-4000-8000-000000000003");
    public static readonly Guid TenantId = Guid.Parse("dd000000-0000-4000-8000-000000000004");
    public static readonly Guid UserId = Guid.Parse("ee000000-0000-4000-8000-000000000005");

    public static OrderAggregate Nueva() => OrderAggregate.Create(
        OrderId, PaymentId, Licencia(), MedioDePago(), Comprador(), Copropiedad(), UserId);

    public static License Licencia()
    {
        var modules = new List<LicenseModule>
        {
            new(Guid.Parse("33000000-0000-4000-8000-000000000008"), "Administracion", "Cuotas y cargos"),
        };

        return License.Create(
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
    public static PaymentMethod MedioDePago() => PaymentMethod.Create(
        "CREDIT_CARD", null,
        CreditCard.Create("tok_test_0001", "4242", "2030/12", "Copropiedad Malpelo", "123", 1));

    public static Buyer Comprador() =>
        Buyer.CreateMinimal(BuyerId, "Copropiedad Malpelo", "+573001234567", "compras@malpelo.com");

    public static Tenant Copropiedad() => Tenant.Create(
        TenantId, "Conjunto Malpelo", null,
        TypeDocument.Create("NIT", "Numero de Identificacion Tributaria"),
        "900123456", "+573001234567", "admin@malpelo.com", Ubicacion());

    public static Location Ubicacion()
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
