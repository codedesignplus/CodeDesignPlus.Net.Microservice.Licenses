using CodeDesignPlus.Net.File.Storage.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using Microsoft.Extensions.Logging;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order.Services;

/// <summary>
/// The email of the purchase as the tests of the payment use it.
/// </summary>
internal static class PurchaseEmailsFixture
{
    /// <summary>
    /// With the platform invoicing off: the email goes when the payment is approved, as before pendings/661 step 4, and the
    /// reservation of the email always succeeds.
    /// </summary>
    public static IPurchaseEmails NotInvoiced(Mock<IOrderRepository> orders, Mock<IPubSub> pubsub, Mock<ICurrencyGrpc> currencies, Mock<IFileStorageGrpc> uploads)
    {
        orders.Setup(x => x.ClaimReceiptEmailAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);

        return new PurchaseEmails(orders.Object, pubsub.Object, currencies.Object, uploads.Object, Mock.Of<IFileStorage>(), MsOptions.Create(new PlatformInvoicingOptions { Enable = false }), NodaTime.SystemClock.Instance, Mock.Of<ILogger<PurchaseEmails>>());
    }
}
