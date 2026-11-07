using CodeDesignPlus.Net.File.Storage.Abstractions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// Signs the purchase receipt of an order for whoever reads it: the buyer (<c>GET Order/{id}/receipt-url</c>) or the
/// platform (<c>GET Order/all/{id}/receipt-url</c>, pendings/674).
/// </summary>
/// <remarks>
/// The signature is asked for with the tenant of the order and not with the caller's: the file lives in that tenant's folder
/// of ms-filestorage, and the Super Admin reads orders of every tenant.
/// </remarks>
public static class ReceiptUrl
{
    /// <summary>
    /// The signature lives just long enough to open the PDF; it is neither stored nor cached.
    /// </summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Issues a fresh signed URL of the order's receipt.
    /// </summary>
    /// <param name="fileStorage">The storage of ms-filestorage.</param>
    /// <param name="order">The order whose receipt is signed.</param>
    /// <param name="cancellationToken">Token to monitor for cancellation requests.</param>
    /// <returns>The signed URL, or <c>null</c> while the order has no receipt or the storage did not sign it.</returns>
    public static async Task<string?> SignAsync(IFileStorage fileStorage, OrderAggregate order, CancellationToken cancellationToken)
    {
        if (order.Receipt is null)
        {
            return null;
        }

        // El recibo vive en {target}/{id}/{nombre}, la carpeta que le da ms-filestorage (regla 56).
        var response = await fileStorage.GetSignedUrlAsync(order.Receipt.Name, $"{order.Receipt.Target}/{order.Receipt.Id}", Lifetime, order.TenantDetail.Id, cancellationToken);

        return response?.Success == true ? response.File.Detail.SignedUrl.ToString() : null;
    }
}
