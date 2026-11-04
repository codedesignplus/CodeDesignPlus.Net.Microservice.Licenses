using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Cache.Abstractions;
using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetAllLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetLicenseById;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License;

/// <summary>
/// The Super Admin reads, in each offer of a tailored license, whether the tenant already took it or is paying for it, so
/// the screen does not offer to withdraw what the service would then reject with 273 or 274 (rule 42, pendings/464).
/// </summary>
public class OfferStatusTest
{
    // The license of the paid order and of the change of the fixtures: a tenant with those orders took it or is paying it.
    private static readonly Guid Tailored = TestOrders.BuildLicense().Id;

    private readonly Mock<ILicenseRepository> licenses = new();
    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<IMapper> mapper = new();
    private readonly Mock<ICacheManager> cache = new();

    public OfferStatusTest()
    {
        orders.Setup(x => x.FindPaidByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
        orders.Setup(x => x.FindAwaitingSubscriptionPaymentAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).ReturnsAsync([]);
    }

    private static LicenseAggregate Offered() => LicenseAggregate.Create(
        Tailored, "Élite Plus", "Plan", "Plan", [],
        [Price.Create(BillingType.Monthly, Money.FromLong(70_000_000, "COP"), BillingModel.FlatRate, 0, TaxTreatment.Taxed, 1900, null)],
        Icon.Create("solar:fire-bold", "#EC4899"), "Terms", [], isActive: true, isPopular: false, showInLandingPage: false,
        "Suscripción al servicio en la nube Kappali (SaaS)", [new OfferedTenant(TestOrders.TenantId, "Las Acacias")], Guid.NewGuid());

    // A new DTO on every call, as Mapster gives: a flag left from another read would hide a missing computation.
    private static LicenseDto Dto() => new()
    {
        Id = Tailored,
        ShowInLandingPage = false,
        OfferedTo = [new LicenseOfferDto { TenantId = TestOrders.TenantId, TenantName = "Las Acacias" }],
    };

    private void TheTenantTookIt() => orders.Setup(x => x.FindPaidByTenantAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([TestOrders.PaidFrom(SubscriptionFixtures.Now)]);

    private void TheTenantIsPayingIt() => orders.Setup(x => x.FindAwaitingSubscriptionPaymentAsync(TestOrders.TenantId, It.IsAny<CancellationToken>())).ReturnsAsync([ChangeValidationFixesTest.EliteChange()]);

    private async Task<LicenseOfferDto> ReadAll()
    {
        var license = Offered();
        licenses.Setup(x => x.MatchingAsync<LicenseAggregate>(It.IsAny<C.Criteria>(), It.IsAny<CancellationToken>())).ReturnsAsync(Pagination<LicenseAggregate>.Create([license], 1, 10, 0));
        mapper.Setup(x => x.Map<LicenseDto>(license)).Returns(Dto());

        var result = await new GetAllLicenseQueryHandler(licenses.Object, orders.Object, mapper.Object).Handle(new GetAllLicenseQuery(new C.Criteria(), ForSaleOnly: false), CancellationToken.None);

        return Assert.Single(Assert.Single(result.Data).OfferedTo);
    }

    private async Task<LicenseOfferDto> ReadById()
    {
        var license = Offered();
        licenses.Setup(x => x.FindAsync<LicenseAggregate>(Tailored, It.IsAny<CancellationToken>())).ReturnsAsync(license);
        mapper.Setup(x => x.Map<LicenseDto>(license)).Returns(Dto());

        var result = await new GetLicenseByIdQueryHandler(licenses.Object, orders.Object, mapper.Object, cache.Object).Handle(new GetLicenseByIdQuery(Tailored, ForSaleOnly: false), CancellationToken.None);

        return Assert.Single(result.OfferedTo);
    }

    /// <summary>
    /// The list of the Super Admin says the tenant already took the license.
    /// </summary>
    [Fact]
    public async Task GetAll_ATenantThatTookIt_IsTaken()
    {
        // Arrange
        TheTenantTookIt();

        // Act
        var offer = await ReadAll();

        // Assert
        Assert.Equal((true, false), (offer.Taken, offer.PaymentInProgress));
    }

    /// <summary>
    /// The list of the Super Admin says the tenant has a change to the license waiting for its payment.
    /// </summary>
    [Fact]
    public async Task GetAll_ATenantPayingIt_HasAPaymentInProgress()
    {
        // Arrange
        TheTenantIsPayingIt();

        // Act
        var offer = await ReadAll();

        // Assert
        Assert.Equal((false, true), (offer.Taken, offer.PaymentInProgress));
    }

    /// <summary>
    /// An offer the tenant neither took nor is paying can be withdrawn: both flags are off.
    /// </summary>
    [Fact]
    public async Task GetAll_ATenantThatDidNotTakeIt_CanBeWithdrawn()
    {
        // Act
        var offer = await ReadAll();

        // Assert
        Assert.Equal((false, false), (offer.Taken, offer.PaymentInProgress));
    }

    /// <summary>
    /// The detail of the Super Admin says the tenant already took the license.
    /// </summary>
    [Fact]
    public async Task GetById_ATenantThatTookIt_IsTaken()
    {
        // Arrange
        TheTenantTookIt();

        // Act
        var offer = await ReadById();

        // Assert
        Assert.True(offer.Taken);
    }

    /// <summary>
    /// The detail of the Super Admin says the tenant has a change to the license waiting for its payment.
    /// </summary>
    [Fact]
    public async Task GetById_ATenantPayingIt_HasAPaymentInProgress()
    {
        // Arrange
        TheTenantIsPayingIt();

        // Act
        var offer = await ReadById();

        // Assert
        Assert.True(offer.PaymentInProgress);
    }

    /// <summary>
    /// The status depends on the orders, not on the license: a detail read from the cache is computed again.
    /// </summary>
    [Fact]
    public async Task GetById_FromTheCache_ComputesTheStatusAgain()
    {
        // Arrange
        TheTenantTookIt();
        cache.Setup(x => x.ExistsAsync(Tailored.ToString())).ReturnsAsync(true);
        cache.Setup(x => x.GetAsync<LicenseDto>(Tailored.ToString())).ReturnsAsync(Dto());

        // Act
        var result = await new GetLicenseByIdQueryHandler(licenses.Object, orders.Object, mapper.Object, cache.Object).Handle(new GetLicenseByIdQuery(Tailored, ForSaleOnly: false), CancellationToken.None);

        // Assert
        Assert.True(Assert.Single(result.OfferedTo).Taken);
    }

    /// <summary>
    /// The public catalog never reads the orders: it has no offers and it is anonymous.
    /// </summary>
    [Fact]
    public async Task GetAll_ThePublicCatalog_DoesNotReadTheOrders()
    {
        // Arrange
        var license = Offered();
        licenses.Setup(x => x.MatchingShownInLandingPageAsync(It.IsAny<C.Criteria>(), It.IsAny<CancellationToken>())).ReturnsAsync(Pagination<LicenseAggregate>.Create([license], 1, 10, 0));
        mapper.Setup(x => x.Map<LicenseDto>(license)).Returns(Dto());

        // Act
        await new GetAllLicenseQueryHandler(licenses.Object, orders.Object, mapper.Object).Handle(new GetAllLicenseQuery(new C.Criteria(), ForSaleOnly: true), CancellationToken.None);

        // Assert
        orders.Verify(x => x.FindPaidByTenantAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
