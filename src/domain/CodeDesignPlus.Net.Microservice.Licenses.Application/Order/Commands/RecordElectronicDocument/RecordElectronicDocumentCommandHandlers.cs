using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.RecordElectronicDocument;

/// <summary>
/// Finds the order of an electronic document by its reference and the document in it by its id (pendings/661).
/// </summary>
/// <remarks>
/// ms-electronicinvoicing publishes the events of every issuer on the same topic, and the platform's documents carry its
/// reserved tenant, not the copropiedad's: the reference is what says whether the document is of an order of this
/// service, and the id which of its documents it is. Anything else is another caller's document and is ignored.
/// </remarks>
internal static class OrderDocuments
{
    /// <summary>
    /// The order and its document, or nothing when the document is not of an order of this service.
    /// </summary>
    public static async Task<(OrderAggregate Order, OrderElectronicDocument Document)?> FindAsync(IOrderRepository orders, PlatformInvoicingOptions options, Guid documentId, string reference, ILogger logger, CancellationToken cancellationToken)
    {
        var orderId = PlatformInvoice.OrderIdOf(options, reference);

        if (orderId is null)
        {
            return null;
        }

        var order = await orders.FindAsync<OrderAggregate>(orderId.Value, cancellationToken);
        var document = order?.ElectronicDocumentOf(documentId);

        if (order is null || document is null)
        {
            logger.LogWarning("Electronic document {DocumentId} with reference {Reference} is not of any order: it is ignored.", documentId, reference);

            return null;
        }

        return (order, document);
    }
}

/// <summary>
/// Keeps on the order the number, CUFE and files of its validated document.
/// </summary>
/// <param name="orders">The repository of orders.</param>
/// <param name="options">How the platform invoices.</param>
/// <param name="logger">The logger.</param>
public class RecordElectronicDocumentValidatedCommandHandler(IOrderRepository orders, IOptions<PlatformInvoicingOptions> options, ILogger<RecordElectronicDocumentValidatedCommandHandler> logger) : IRequestHandler<RecordElectronicDocumentValidatedCommand>
{
    /// <inheritdoc/>
    public async Task Handle(RecordElectronicDocumentValidatedCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var found = await OrderDocuments.FindAsync(orders, options.Value, request.DocumentId, request.Reference, logger, cancellationToken);

        if (found is not { } match)
        {
            return;
        }

        var validated = match.Document.Validate(request.Prefix, request.Consecutive, request.Cufe, request.IssueDate, request.ValidatedAt, request.Xml, request.Pdf);

        if (await orders.RecordElectronicDocumentAsync(match.Order.Id, validated, cancellationToken))
        {
            logger.LogInformation("Electronic document {Number} ({DocumentId}) of order {OrderId} was validated.", validated.Number, validated.DocumentId, match.Order.Id);
        }
    }
}

/// <summary>
/// Keeps on the order why its document was rejected and tells the platform administrator, who corrects it and sends it
/// again (rule 55). The purchase is not undone: the copropiedad already has its license.
/// </summary>
/// <param name="orders">The repository of orders.</param>
/// <param name="inbox">The inbox.</param>
/// <param name="options">How the platform invoices.</param>
/// <param name="notices">To whom the platform notices go.</param>
/// <param name="logger">The logger.</param>
public class RecordElectronicDocumentRejectedCommandHandler(IOrderRepository orders, IInboxGrpc inbox, IOptions<PlatformInvoicingOptions> options, IOptions<PlatformNoticeOptions> notices, ILogger<RecordElectronicDocumentRejectedCommandHandler> logger) : IRequestHandler<RecordElectronicDocumentRejectedCommand>
{
    /// <inheritdoc/>
    public async Task Handle(RecordElectronicDocumentRejectedCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var found = await OrderDocuments.FindAsync(orders, options.Value, request.DocumentId, request.Reference, logger, cancellationToken);

        if (found is not { } match)
        {
            return;
        }

        var rejected = match.Document.Reject(request.Prefix, request.Consecutive, request.Reasons, request.RejectedAt);

        // A late rejection of a document already validated changes nothing and tells nobody.
        if (!await orders.RecordElectronicDocumentAsync(match.Order.Id, rejected, cancellationToken))
        {
            return;
        }

        logger.LogWarning("Electronic document {DocumentId} of order {OrderId} was rejected: {Reasons}", rejected.DocumentId, match.Order.Id, string.Join("; ", rejected.Reasons));

        var role = notices.Value.PlatformAdministratorRole;

        // Without the role the notice would reach nobody: fail so the message is retried and it shows in the log.
        if (role == Guid.Empty)
        {
            throw new InvalidOperationException($"'{PlatformNoticeOptions.Section}:{nameof(PlatformNoticeOptions.PlatformAdministratorRole)}' is not configured: the rejected document {rejected.DocumentId} of order {match.Order.Id} cannot be notified.");
        }

        var order = match.Order;
        var isCreditNote = rejected.CorrectedOrderId is not null;
        var payload = new { orderId = order.Id, documentId = rejected.DocumentId, tenantId = order.TenantDetail.Id, tenantName = order.TenantDetail.Name, origin = request.ByTaxAuthority ? "dian" : "provider", reasons = rejected.Reasons, rejectedAt = request.RejectedAt };

        await inbox.NotifyRolesAsync([role.ToString()], PlatformNotificationKinds.LicenseInvoiceRejected, ElectronicInvoiceText.RejectedTitle, ElectronicInvoiceText.Rejected(order.TenantDetail.Name, isCreditNote, request.ByTaxAuthority, rejected.Reasons), OrderNotAppliedText.Module, order.Id.ToString(), payload, Guid.Empty, Guid.Empty, request.RejectedAt.ToDateTimeOffset(), NotificationIds.For(request.EventId, PlatformNotificationKinds.LicenseInvoiceRejected), cancellationToken);
    }
}
