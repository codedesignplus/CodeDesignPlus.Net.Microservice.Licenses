using CodeDesignPlus.Net.Exceptions;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.IssueOrderInvoice;

/// <summary>
/// Asks ms-electronicinvoicing for the documents of a paid order: first a credit note on the invoice of each order whose
/// unused period it credits, then the invoice of what it paid (pendings/661, 738, 739).
/// </summary>
/// <remarks>
/// <para>
/// It runs from the events that follow the payment, never inside it: the copropiedad gets its license whatever happens
/// here, and a failure here retries the message without touching the purchase.
/// </para>
/// <para>
/// The documents are written as requested on the order before calling ms-electronicinvoicing, so its answer always finds
/// them; and they are written only once, so a redelivery cannot turn a validated invoice back into a requested one. Their
/// ids are derived from the order, so calling again returns the same documents instead of issuing new ones.
/// </para>
/// <para>
/// A credit note needs the validated invoice it corrects. If that invoice is still waiting for the tax authority, the
/// message fails and is retried later. If it is not in the system (invoiced by hand or before pendings/661) or it was
/// rejected, the note is not sent: it stays for review and the platform administrator is told. The invoice of the order is
/// issued anyway: the sale happened.
/// </para>
/// <para>
/// The buyer's document is checked before asking for anything (Kappali pendings/744). A renewal is never refused for it,
/// so an order can be paid by a copropiedad whose document type carries a check digit and comes without it or with a wrong
/// one. Nothing is guessed: neither the invoice nor its credit notes are sent, they stay for review with the reason and the
/// platform administrator is told. Issuing them once the document is corrected is done by hand.
/// </para>
/// </remarks>
/// <param name="orders">The repository of orders.</param>
/// <param name="invoicing">The client of ms-electronicinvoicing.</param>
/// <param name="inbox">The inbox, to tell the platform administrator.</param>
/// <param name="options">How the platform invoices.</param>
/// <param name="notices">To whom the platform notices go.</param>
/// <param name="tenantDocument">Which document types carry a check digit and how it is computed.</param>
/// <param name="logger">The logger.</param>
public class IssueOrderInvoiceCommandHandler(IOrderRepository orders, IElectronicInvoicingGrpc invoicing, IInboxGrpc inbox, IOptions<PlatformInvoicingOptions> options, IOptions<PlatformNoticeOptions> notices, IOptions<TenantDocumentOptions> tenantDocument, ILogger<IssueOrderInvoiceCommandHandler> logger) : IRequestHandler<IssueOrderInvoiceCommand>
{
    /// <inheritdoc/>
    public async Task Handle(IssueOrderInvoiceCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var settings = options.Value;

        if (!settings.Enable)
        {
            logger.LogInformation("The platform invoicing is off: order {OrderId} is not invoiced.", request.OrderId);

            return;
        }

        var order = await orders.FindAsync<OrderAggregate>(request.OrderId, cancellationToken);

        ApplicationGuard.IsNull(order, Errors.OrderNotFound);

        if (!IsInvoiceable(order))
        {
            return;
        }

        var corrected = await CorrectedOrdersAsync(order, cancellationToken);

        if (order.Invoice is null)
        {
            order = await RequestAsync(order, corrected, settings, cancellationToken);
        }

        var notes = order.CreditNotes ?? [];

        foreach (var note in notes.Where(x => x.State == ElectronicDocumentState.Requested))
        {
            var correctedOrder = corrected[note.CorrectedOrderId!.Value];

            await invoicing.IssueDocumentAsync(PlatformInvoice.CreditNote(order, correctedOrder, correctedOrder.Invoice!, settings), cancellationToken);

            logger.LogInformation("Credit note {DocumentId} of order {OrderId} on the invoice of order {CorrectedOrderId} was requested.", note.DocumentId, order.Id, correctedOrder.Id);
        }

        if (order.Invoice!.State != ElectronicDocumentState.Requested)
        {
            return;
        }

        // With a single note the invoice waits for it, so the books read the correction before the new sale.
        var issued = notes.Where(x => x.State != ElectronicDocumentState.NeedsReview).ToList();
        Guid? precededBy = issued.Count == 1 ? issued[0].DocumentId : null;

        await invoicing.IssueDocumentAsync(PlatformInvoice.Invoice(order, corrected, settings, precededBy), cancellationToken);

        logger.LogInformation("Invoice {DocumentId} of order {OrderId} was requested.", order.Invoice.DocumentId, order.Id);
    }

    private bool IsInvoiceable(OrderAggregate order)
    {
        if (order.PaymentStatus != PaymentStatus.Succeeded || order.PeriodStart is null)
        {
            logger.LogWarning("Order {OrderId} is not paid: it is not invoiced.", order.Id);

            return false;
        }

        if (order.SupersededBy is not null)
        {
            // What was paid became balance in favour and sold nothing: it is invoiced with the order that uses it.
            logger.LogInformation("Order {OrderId} was superseded by {SupersededBy}: it is not invoiced.", order.Id, order.SupersededBy);

            return false;
        }

        return true;
    }

    private async Task<Dictionary<Guid, OrderAggregate>> CorrectedOrdersAsync(OrderAggregate order, CancellationToken cancellationToken)
    {
        var corrected = new Dictionary<Guid, OrderAggregate>();

        foreach (var id in PlatformInvoice.CorrectedOrderIdsOf(order))
        {
            var previous = await orders.FindAsync<OrderAggregate>(id, cancellationToken);

            if (previous is not null)
            {
                corrected[id] = previous;
            }
        }

        return corrected;
    }

    private async Task<OrderAggregate> RequestAsync(OrderAggregate order, Dictionary<Guid, OrderAggregate> corrected, PlatformInvoicingOptions settings, CancellationToken cancellationToken)
    {
        var now = SystemClock.Instance.GetCurrentInstant();
        var notes = new List<OrderElectronicDocument>();
        var invoiceId = PlatformInvoice.InvoiceIdOf(order.Id);
        var invoiceReference = PlatformInvoice.InvoiceReferenceOf(settings, order.Id);

        // Every document of the order carries the same buyer: with a document that is not valid none of them is sent.
        if (BuyerDocumentProblemOf(order) is { } problem)
        {
            notes.AddRange(PlatformInvoice.CorrectedOrderIdsOf(order).Select(id => OrderElectronicDocument.ForReview(PlatformInvoice.CreditNoteIdOf(order.Id, id), PlatformInvoice.CreditNoteReferenceOf(settings, order.Id, id), id, problem, now)));

            order.RequestElectronicDocuments(OrderElectronicDocument.ForReview(invoiceId, invoiceReference, null, problem, now), notes);

            if (!await orders.RequestElectronicDocumentsAsync(order, cancellationToken))
            {
                // Another delivery wrote them first: carry on with what it wrote.
                return await orders.FindAsync<OrderAggregate>(order.Id, cancellationToken) ?? order;
            }

            await NotifyInvoiceReviewAsync(order, order.Invoice!, cancellationToken);

            return order;
        }

        foreach (var id in PlatformInvoice.CorrectedOrderIdsOf(order))
        {
            notes.Add(NoteFor(order, id, corrected.GetValueOrDefault(id), settings, now));
        }

        order.RequestElectronicDocuments(OrderElectronicDocument.Requested(invoiceId, invoiceReference, null, now), notes);

        if (!await orders.RequestElectronicDocumentsAsync(order, cancellationToken))
        {
            // Another delivery wrote them first: carry on with what it wrote.
            return await orders.FindAsync<OrderAggregate>(order.Id, cancellationToken) ?? order;
        }

        foreach (var note in notes.Where(x => x.State == ElectronicDocumentState.NeedsReview))
        {
            await NotifyReviewAsync(order, note, cancellationToken);
        }

        return order;
    }

    /// <summary>
    /// What is wrong with the document of the copropiedad the order carries, in the words the platform administrator reads;
    /// null when it can be invoiced.
    /// </summary>
    private string? BuyerDocumentProblemOf(OrderAggregate order)
    {
        var tenant = order.TenantDetail;

        try
        {
            tenantDocument.Value.ToRules().Ensure(tenant.TypeDocument?.Code, tenant.NumberDocument);

            return null;
        }
        catch (CodeDesignPlusException exception)
        {
            return ElectronicInvoiceText.BuyerDocumentIsNotValid(tenant.NumberDocument, exception.Error?.GetMessage(ElectronicInvoiceText.Language) ?? exception.Message);
        }
    }

    private static OrderElectronicDocument NoteFor(OrderAggregate order, Guid correctedOrderId, OrderAggregate? corrected, PlatformInvoicingOptions settings, Instant now)
    {
        var id = PlatformInvoice.CreditNoteIdOf(order.Id, correctedOrderId);
        var reference = PlatformInvoice.CreditNoteReferenceOf(settings, order.Id, correctedOrderId);

        return corrected?.Invoice?.State switch
        {
            ElectronicDocumentState.Validated => OrderElectronicDocument.Requested(id, reference, correctedOrderId, now),
            // The tax authority has not answered yet: the note waits for it, retrying the message.
            ElectronicDocumentState.Requested => throw new InvalidOperationException($"The invoice of order {correctedOrderId} is not validated yet: the credit note of order {order.Id} waits for it."),
            ElectronicDocumentState.Rejected => OrderElectronicDocument.ForReview(id, reference, correctedOrderId, ElectronicInvoiceText.InvoiceRejected(correctedOrderId), now),
            _ => OrderElectronicDocument.ForReview(id, reference, correctedOrderId, ElectronicInvoiceText.WithoutInvoice(correctedOrderId), now),
        };
    }

    private async Task NotifyInvoiceReviewAsync(OrderAggregate order, OrderElectronicDocument invoice, CancellationToken cancellationToken)
    {
        var role = notices.Value.PlatformAdministratorRole;
        var reason = invoice.Reasons.FirstOrDefault() ?? string.Empty;

        logger.LogWarning("Invoice {DocumentId} of order {OrderId} was not sent: {Reason}", invoice.DocumentId, order.Id, reason);

        if (role == Guid.Empty)
        {
            logger.LogError("'{Section}:{Option}' is not configured: the invoice {DocumentId} waiting for review is not notified.", PlatformNoticeOptions.Section, nameof(PlatformNoticeOptions.PlatformAdministratorRole), invoice.DocumentId);

            return;
        }

        var payload = new { orderId = order.Id, documentId = invoice.DocumentId, tenantId = order.TenantDetail.Id, tenantName = order.TenantDetail.Name, reasons = invoice.Reasons };

        await inbox.NotifyRolesAsync([role.ToString()], PlatformNotificationKinds.LicenseInvoiceRejected, ElectronicInvoiceText.InvoiceReviewTitle, ElectronicInvoiceText.InvoiceReview(order.TenantDetail.Name, reason), OrderNotAppliedText.Module, order.Id.ToString(), payload, Guid.Empty, Guid.Empty, invoice.UpdatedAt.ToDateTimeOffset(), NotificationIds.For(invoice.DocumentId, PlatformNotificationKinds.LicenseInvoiceRejected), cancellationToken);
    }

    private async Task NotifyReviewAsync(OrderAggregate order, OrderElectronicDocument note, CancellationToken cancellationToken)
    {
        var role = notices.Value.PlatformAdministratorRole;
        var reason = note.Reasons.FirstOrDefault() ?? string.Empty;

        logger.LogWarning("Credit note {DocumentId} of order {OrderId} was not sent: {Reason}", note.DocumentId, order.Id, reason);

        if (role == Guid.Empty)
        {
            logger.LogError("'{Section}:{Option}' is not configured: the credit note {DocumentId} waiting for review is not notified.", PlatformNoticeOptions.Section, nameof(PlatformNoticeOptions.PlatformAdministratorRole), note.DocumentId);

            return;
        }

        var payload = new { orderId = order.Id, documentId = note.DocumentId, correctedOrderId = note.CorrectedOrderId, tenantId = order.TenantDetail.Id, tenantName = order.TenantDetail.Name, reasons = note.Reasons };

        await inbox.NotifyRolesAsync([role.ToString()], PlatformNotificationKinds.LicenseInvoiceRejected, ElectronicInvoiceText.ReviewTitle, ElectronicInvoiceText.Review(order.TenantDetail.Name, reason), OrderNotAppliedText.Module, order.Id.ToString(), payload, Guid.Empty, Guid.Empty, note.UpdatedAt.ToDateTimeOffset(), NotificationIds.For(note.DocumentId, PlatformNotificationKinds.LicenseInvoiceRejected), cancellationToken);
    }
}
