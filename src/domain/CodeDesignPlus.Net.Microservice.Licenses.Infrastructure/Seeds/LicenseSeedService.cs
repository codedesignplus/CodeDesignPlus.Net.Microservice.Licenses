using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;
using Microsoft.Extensions.Hosting;

namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Seeds;

/// <summary>
/// BackgroundService that seeds the 4 license tiers (Free, Basic, Premium, Ultimate)
/// on application startup. Idempotent: if licenses already exist, skips creation.
/// </summary>
public class LicenseSeedService(
    IServiceScopeFactory serviceScopeFactory,
    ILogger<LicenseSeedService> logger) : BackgroundService
{
    private static readonly Guid SystemUserId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly string CurrencyCode = "COP";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Verificando seed de licencias...");

        using var scope = serviceScopeFactory.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<ILicenseRepository>();

        var criteria = new CodeDesignPlus.Net.Core.Abstractions.Models.Criteria.Criteria { Limit = 1 };
        var existing = await repository.MatchingAsync<LicenseAggregate>(criteria, stoppingToken);

        if (existing.TotalCount > 0)
        {
            logger.LogInformation("Licencias ya existen ({Count}). Omitiendo seed.", existing.TotalCount);
            return;
        }

        await SeedFreeLicenseAsync(repository, stoppingToken);
        await SeedBasicLicenseAsync(repository, stoppingToken);
        await SeedPremiumLicenseAsync(repository, stoppingToken);
        await SeedUltimateLicenseAsync(repository, stoppingToken);

        logger.LogInformation("4 licencias sembradas exitosamente.");
    }

    private async Task SeedFreeLicenseAsync(ILicenseRepository repository, CancellationToken ct)
    {
        var modules = GetAllModules();

        var prices = new List<Price>
        {
            Price.Create(BillingType.None, Money.FromLong(0, CurrencyCode), BillingModel.None, 0, 0)
        };

        var attributes = new Dictionary<string, string>
        {
            ["TrialDays"] = "20",
            ["Tier"] = "Free"
        };

        var license = LicenseAggregate.Create(
            Guid.NewGuid(),
            "Free",
            "Prueba gratuita de 20 dias con acceso completo a todas las funcionalidades.",
            "Plan gratuito de prueba que incluye todos los modulos de la plataforma por 20 dias. Ideal para evaluar Kappali antes de elegir un plan de pago.",
            modules,
            prices,
            Icon.Create("rocket", "#10B981"),
            "Terminos y condiciones del plan Free aplican. El acceso se suspende automaticamente despues de 20 dias.",
            attributes,
            true,
            false,
            true,
            SystemUserId);

        await repository.CreateAsync(license, ct);

        logger.LogInformation("Licencia Free creada.");
    }

    private async Task SeedBasicLicenseAsync(ILicenseRepository repository, CancellationToken ct)
    {
        var modules = GetCoreModules();
        modules.AddRange(GetFinancialModules());

        var prices = new List<Price>
        {
            Price.Create(BillingType.Monthly, Money.FromLong(15000000, CurrencyCode), BillingModel.FlatRate, 0, 19),
            Price.Create(BillingType.Annually, Money.FromLong(150000000, CurrencyCode), BillingModel.FlatRate, 16, 19)
        };

        var attributes = new Dictionary<string, string>
        {
            ["Tier"] = "Basic"
        };

        var license = LicenseAggregate.Create(
            Guid.NewGuid(),
            "Basic",
            "Gestion basica de propiedad horizontal con finanzas incluidas.",
            "Incluye la gestion de estructura fisica (fases, bloques, torres, unidades), residentes, contabilidad, facturacion y administracion de cuotas. Ideal para conjuntos pequenos.",
            modules,
            prices,
            Icon.Create("building", "#3B82F6"),
            "Terminos y condiciones del plan Basic. Facturacion mensual o anual con descuento del 16%.",
            attributes,
            true,
            false,
            true,
            SystemUserId);

        await repository.CreateAsync(license, ct);

        logger.LogInformation("Licencia Basic creada.");
    }

    private async Task SeedPremiumLicenseAsync(ILicenseRepository repository, CancellationToken ct)
    {
        var modules = GetCoreModules();
        modules.AddRange(GetFinancialModules());
        modules.AddRange(GetPremiumModules());

        var prices = new List<Price>
        {
            Price.Create(BillingType.Monthly, Money.FromLong(35000000, CurrencyCode), BillingModel.FlatRate, 0, 19),
            Price.Create(BillingType.Annually, Money.FromLong(350000000, CurrencyCode), BillingModel.FlatRate, 16, 19)
        };

        var attributes = new Dictionary<string, string>
        {
            ["Tier"] = "Premium"
        };

        var license = LicenseAggregate.Create(
            Guid.NewGuid(),
            "Premium",
            "Gestion completa con areas comunes, parqueadero y depositos.",
            "Todo lo del plan Basic mas la gestion de areas comunes con reservas, parqueadero visitante y mensual, y depositos. Ideal para conjuntos medianos con servicios compartidos.",
            modules,
            prices,
            Icon.Create("star", "#F59E0B"),
            "Terminos y condiciones del plan Premium. Facturacion mensual o anual con descuento del 16%.",
            attributes,
            true,
            true,
            true,
            SystemUserId);

        await repository.CreateAsync(license, ct);

        logger.LogInformation("Licencia Premium creada.");
    }

    private async Task SeedUltimateLicenseAsync(ILicenseRepository repository, CancellationToken ct)
    {
        var modules = GetAllModules();

        var prices = new List<Price>
        {
            Price.Create(BillingType.Monthly, Money.FromLong(50000000, CurrencyCode), BillingModel.FlatRate, 0, 19),
            Price.Create(BillingType.Annually, Money.FromLong(500000000, CurrencyCode), BillingModel.FlatRate, 16, 19)
        };

        var attributes = new Dictionary<string, string>
        {
            ["Tier"] = "Ultimate"
        };

        var license = LicenseAggregate.Create(
            Guid.NewGuid(),
            "Ultimate",
            "Acceso completo a todos los modulos de la plataforma.",
            "Incluye absolutamente todos los modulos: estructura, residentes, finanzas, areas comunes, parqueadero, depositos, contratos de arrendamiento y vehiculos. Ideal para conjuntos grandes con todos los servicios.",
            modules,
            prices,
            Icon.Create("crown", "#8B5CF6"),
            "Terminos y condiciones del plan Ultimate. Facturacion mensual o anual con descuento del 16%.",
            attributes,
            true,
            false,
            true,
            SystemUserId);

        await repository.CreateAsync(license, ct);

        logger.LogInformation("Licencia Ultimate creada.");
    }

    private static List<ModuleEntity> GetCoreModules() =>
    [
        new() { Id = Guid.Parse("84ef68f7-7172-45c8-a42f-963d5e94e5bf"), Name = "Organization", Description = "Physical and legal identity of the property" },
        new() { Id = Guid.Parse("9cd56582-2d08-4484-9e5f-d65a05223750"), Name = "Phases", Description = "Construction phases that group blocks and towers" },
        new() { Id = Guid.Parse("91eef1de-64d0-467a-b0df-6fc0df2375dc"), Name = "Blocks", Description = "Physical blocks that group towers or units" },
        new() { Id = Guid.Parse("afbffa12-9c67-4b22-ac33-6789b0cbec14"), Name = "Towers", Description = "Vertical towers that contain units" },
        new() { Id = Guid.Parse("97516de1-1bd1-4bda-a555-226c3fc0c0a3"), Name = "Units", Description = "Individual residential or commercial units" },
        new() { Id = Guid.Parse("8769ecf8-680a-4278-ac7f-cee67c61b5b6"), Name = "Residents", Description = "Registry of residents and owners" },
    ];

    private static List<ModuleEntity> GetFinancialModules() =>
    [
        new() { Id = Guid.Parse("d2d9b66b-5424-4d5b-a20d-488dbf18ef53"), Name = "Accounting", Description = "Chart of accounts, taxes, and accounting entries" },
        new() { Id = Guid.Parse("493010e5-25cf-41c4-a82c-371ce533a209"), Name = "Invoicing", Description = "Invoice generation and payment reconciliation" },
        new() { Id = Guid.Parse("27a97b1f-ad4a-46e4-837e-6bb9a368f040"), Name = "Administration", Description = "Administration fees, reserve fund, and assessments" },
    ];

    private static List<ModuleEntity> GetPremiumModules() =>
    [
        new() { Id = Guid.Parse("9fe4ffac-5d17-4c16-a43e-8eabc8c11252"), Name = "CommonAreas", Description = "Bookable shared spaces with reservation fees" },
        new() { Id = Guid.Parse("bb2c40d4-4dc1-401e-a3f6-49d4c76d65e0"), Name = "Parking", Description = "Visitor parking rates and monthly leases" },
        new() { Id = Guid.Parse("f49bab95-15e1-4174-9268-3ae0f1a89d1a"), Name = "Deposits", Description = "Storage unit deposits with monthly rental" },
    ];

    private static List<ModuleEntity> GetAllModules()
    {
        var all = GetCoreModules();
        all.AddRange(GetFinancialModules());
        all.AddRange(GetPremiumModules());
        all.AddRange(
        [
            new() { Id = Guid.Parse("77b0ba11-ad2a-4cef-b571-3d73825ca3dd"), Name = "LeaseAgreements", Description = "Residential and commercial lease agreements" },
            new() { Id = Guid.Parse("26e7b3e8-eb8b-43b6-83bd-e546e0f950ec"), Name = "Vehicles", Description = "Vehicle registry and access passes" },
        ]);

        return all;
    }
}
