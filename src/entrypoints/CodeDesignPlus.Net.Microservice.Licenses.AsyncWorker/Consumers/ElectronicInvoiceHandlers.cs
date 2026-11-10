using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.IssueOrderInvoice;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.RecordElectronicDocument;
using CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using MediatR;
using StoredFile = CodeDesignPlus.Net.ValueObjects.FileServer.File;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Consumers;

/// <summary>
/// A purchase was paid: its invoice is asked of ms-electronicinvoicing (pendings/661). It runs apart from the provisioning,
/// so the copropiedad is created whatever happens to the invoice.
/// </summary>
/// <param name="mediator">The mediator.</param>
[QueueName<OrderAggregate>("IssuePurchaseInvoiceHandler")]
public class IssuePurchaseInvoiceHandler(IMediator mediator) : IEventHandler<OrderPaidAndReadyForProvisioningDomainEvent>
{
    /// <inheritdoc/>
    public Task HandleAsync(OrderPaidAndReadyForProvisioningDomainEvent data, CancellationToken token) => mediator.Send(new IssueOrderInvoiceCommand(data.AggregateId), token);
}

/// <summary>
/// A renewal was paid: its invoice is asked of ms-electronicinvoicing (pendings/661, 735).
/// </summary>
/// <param name="mediator">The mediator.</param>
[QueueName<OrderAggregate>("IssueRenewalInvoiceHandler")]
public class IssueRenewalInvoiceHandler(IMediator mediator) : IEventHandler<SubscriptionRenewedDomainEvent>
{
    /// <inheritdoc/>
    public Task HandleAsync(SubscriptionRenewedDomainEvent data, CancellationToken token) => mediator.Send(new IssueOrderInvoiceCommand(data.AggregateId), token);
}

/// <summary>
/// A license change was paid: the credit note on the invoice of the period not served and its invoice are asked of
/// ms-electronicinvoicing (pendings/661, 738).
/// </summary>
/// <param name="mediator">The mediator.</param>
[QueueName<OrderAggregate>("IssueChangeInvoiceHandler")]
public class IssueChangeInvoiceHandler(IMediator mediator) : IEventHandler<SubscriptionChangedDomainEvent>
{
    /// <inheritdoc/>
    public Task HandleAsync(SubscriptionChangedDomainEvent data, CancellationToken token) => mediator.Send(new IssueOrderInvoiceCommand(data.AggregateId), token);
}

/// <summary>
/// A package was bought or upgraded: the credit note on the invoice of what is replaced and its invoice are asked of
/// ms-electronicinvoicing (pendings/661, 739).
/// </summary>
/// <param name="mediator">The mediator.</param>
[QueueName<OrderAggregate>("IssuePackageInvoiceHandler")]
public class IssuePackageInvoiceHandler(IMediator mediator) : IEventHandler<SubscriptionPackagesPurchasedDomainEvent>
{
    /// <inheritdoc/>
    public Task HandleAsync(SubscriptionPackagesPurchasedDomainEvent data, CancellationToken token) => mediator.Send(new IssueOrderInvoiceCommand(data.AggregateId), token);
}

/// <summary>
/// The tax authority validated an electronic document: if it is of an order, the order keeps its number, CUFE and files
/// (pendings/661).
/// </summary>
/// <param name="mediator">The mediator.</param>
[QueueName<OrderAggregate>("ElectronicDocumentValidatedHandler")]
public class ElectronicDocumentValidatedHandler(IMediator mediator) : IEventHandler<ElectronicDocumentValidatedDomainEvent>
{
    /// <inheritdoc/>
    public Task HandleAsync(ElectronicDocumentValidatedDomainEvent data, CancellationToken token) =>
        mediator.Send(new RecordElectronicDocumentValidatedCommand(data.AggregateId, data.Reference, data.Prefix, data.Consecutive, data.Cufe, data.IssueDate, data.ValidatedAt, Attachments.Of(data.Xml), Attachments.Of(data.Pdf), Attachments.Of(data.AttachedDocument)), token);
}

/// <summary>
/// ms-electronicinvoicing kept the files of a document after validating it: if it is of an order, the order keeps them and
/// the email of the purchase that waited for them goes (pendings/661, step 4).
/// </summary>
/// <param name="mediator">The mediator.</param>
[QueueName<OrderAggregate>("ElectronicDocumentFilesKeptHandler")]
public class ElectronicDocumentFilesKeptHandler(IMediator mediator) : IEventHandler<ElectronicDocumentFilesKeptDomainEvent>
{
    /// <inheritdoc/>
    public Task HandleAsync(ElectronicDocumentFilesKeptDomainEvent data, CancellationToken token)
    {
        var xml = Attachments.Of(data.Xml);

        // Without the XML there is nothing to keep: ms-electronicinvoicing only announces it with the XML.
        return xml is null ? Task.CompletedTask : mediator.Send(new RecordElectronicDocumentFilesCommand(data.AggregateId, data.Reference, xml, Attachments.Of(data.AttachedDocument), Attachments.Of(data.Pdf), data.OccurredAt), token);
    }
}

/// <summary>
/// The files of ms-electronicinvoicing as the order keeps them.
/// </summary>
internal static class Attachments
{
    /// <summary>
    /// The reference of a stored file, or null when it is not complete.
    /// </summary>
    /// <param name="file">The file as the event brings it.</param>
    /// <returns>The reference.</returns>
    public static FileAttachment? Of(StoredFile? file) => file is null || file.Id == Guid.Empty || string.IsNullOrWhiteSpace(file.Name) || string.IsNullOrWhiteSpace(file.Target)
        ? null
        : new FileAttachment(file.Id, file.Name, file.Target);
}

/// <summary>
/// The provider or the tax authority rejected an electronic document: if it is of an order, the order keeps why and the
/// platform administrator is told (pendings/661).
/// </summary>
/// <param name="mediator">The mediator.</param>
[QueueName<OrderAggregate>("ElectronicDocumentRejectedHandler")]
public class ElectronicDocumentRejectedHandler(IMediator mediator) : IEventHandler<ElectronicDocumentRejectedDomainEvent>
{
    /// <inheritdoc/>
    public Task HandleAsync(ElectronicDocumentRejectedDomainEvent data, CancellationToken token)
    {
        var rejection = data.Rejection;

        return mediator.Send(new RecordElectronicDocumentRejectedCommand(data.AggregateId, data.Reference, data.Prefix, data.Consecutive, rejection?.Origin == ElectronicDocumentRejection.TaxAuthority, rejection?.Reasons ?? [], rejection?.At ?? data.OccurredAt, data.EventId), token);
    }
}
