using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using TenantContracts = CodeDesignPlus.Net.gRpc.Clients.Services.Tenant;
using TenantSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.Tenant;

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

    /// <summary>
    /// The tenant as ms-tenants answers it over gRPC.
    /// </summary>
    public static TenantContracts.GetTenantResponse AnswerOf(TenantSnapshot tenant)
    {
        var location = tenant.Location;
        var currency = location.Country.Currency;

        var answer = new TenantContracts.Location
        {
            Country = new TenantContracts.Country
            {
                Id = location.Country.Id.ToString(),
                Name = location.Country.Name,
                Alpha2 = location.Country.Alpha2,
                Alpha3 = location.Country.Alpha3,
                Code = location.Country.Code,
                PhoneCode = location.Country.PhoneCode,
                Timezone = location.Country.Timezone,
                Currency = new TenantContracts.Currency { Id = currency.CurrencyId.ToString(), Code = currency.Code, Name = currency.Name, Symbol = currency.Symbol, DecimalDigits = currency.DecimalDigits, NumericCode = currency.NumericCode }
            },
            State = new TenantContracts.State { Id = location.State.Id.ToString(), Name = location.State.Name, Code = location.State.Code },
            City = new TenantContracts.City { Id = location.City.Id.ToString(), Name = location.City.Name, Timezone = location.City.Timezone },
            Address = location.Address,
            PostalCode = location.PostalCode
        };

        if (location.Locality is { } locality)
        {
            answer.Locality = new TenantContracts.Locality { Id = locality.Id.ToString(), Name = locality.Name };
        }

        if (location.Neighborhood is { } neighborhood)
        {
            answer.Neighborhood = new TenantContracts.Neighborhood { Id = neighborhood.Id.ToString(), Name = neighborhood.Name };
        }

        return new TenantContracts.GetTenantResponse
        {
            Id = tenant.Id.ToString(),
            Name = tenant.Name,
            TypeDocument = new TenantContracts.TypeDocument { Code = tenant.TypeDocument.Code, Name = tenant.TypeDocument.Name },
            NumberDocument = tenant.NumberDocument,
            Domain = tenant.Web ?? string.Empty,
            Phone = tenant.Phone,
            Email = tenant.Email,
            Location = answer,
            IsActive = true
        };
    }

    /// <summary>
    /// The answer of ms-tenants for the tenant of the tests, written field by field as its mapping from TenantDto leaves it
    /// (CodeDesignPlus.Net.Microservice.Tenants, gRpc/Core/Mapster/MapsterConfig.cs): every id as the text of a Guid, the
    /// website as a Uri prints it (with its final slash), the time zone of the city in its wrapper, no neighborhood, and
    /// the license with its modules. It is not built from the value object of this service on purpose: built from it, the
    /// tests could not see a field ms-tenants leaves empty, and on 2026-11-23 that was the id of the currency.
    /// </summary>
    public static TenantContracts.GetTenantResponse AnswerAsMsTenantsSendsIt()
    {
        var license = new TenantContracts.License { Id = "44000000-0000-4000-8000-000000000109", Name = "Esencial", StartDate = "2026-10-23T15:00:00Z", EndDate = "2026-11-23T15:00:00Z" };

        license.Modules.Add(new TenantContracts.LicenseModule { Id = "33000000-0000-4000-8000-000000000108", Name = "Organización" });

        return new TenantContracts.GetTenantResponse
        {
            Id = TestOrders.TenantId.ToString(),
            Name = "Conjunto Las Acacias",
            TypeDocument = new TenantContracts.TypeDocument { Code = "31", Name = "NIT" },
            NumberDocument = "901777333-0",
            Domain = "https://acacias.example.com/",
            Phone = "+573009876543",
            Email = "administracion@acacias.example.com",
            License = license,
            Location = new TenantContracts.Location
            {
                Country = new TenantContracts.Country
                {
                    Id = "66000000-0000-4000-8000-00000000010b",
                    Name = "Colombia",
                    Alpha2 = "CO",
                    Alpha3 = "COL",
                    Code = 170,
                    PhoneCode = "+57",
                    Timezone = "America/Bogota",
                    Currency = new TenantContracts.Currency { Id = "1e8f4d27-0000-4000-8000-00000000010a", Code = "COP", Name = "Peso Colombiano", Symbol = "$", DecimalDigits = 2, NumericCode = 170 }
                },
                State = new TenantContracts.State { Id = "77000000-0000-4000-8000-00000000010c", Name = "Bogotá, D.C.", Code = "11" },
                City = new TenantContracts.City { Id = "88000000-0000-4000-8000-00000000010d", Name = "Bogotá, D.C.", Timezone = "America/Bogota" },
                Locality = new TenantContracts.Locality { Id = "99000000-0000-4000-8000-00000000010e", Name = "Chapinero" },
                Address = "Carrera 7 # 71-21",
                PostalCode = "110111"
            },
            IsActive = true
        };
    }

    /// <summary>
    /// Makes ms-tenants answer with that tenant when it is asked for its id, and only for its id.
    /// </summary>
    public static void Has(this Mock<ITenantGrpc> grpc, TenantContracts.GetTenantResponse answer)
    {
        grpc.Setup(x => x.GetTenantByIdAsync(It.Is<TenantContracts.GetTenantRequest>(r => r.Id == answer.Id), It.IsAny<CancellationToken>())).ReturnsAsync(answer);
    }

    /// <summary>
    /// The real reader of the tenant over an ms-tenants that has that one.
    /// </summary>
    public static ITenantSnapshots TenantsWith(TenantSnapshot tenant)
    {
        var grpc = new Mock<ITenantGrpc>();

        grpc.Has(AnswerOf(tenant));

        return new TenantSnapshots(grpc.Object);
    }
}
