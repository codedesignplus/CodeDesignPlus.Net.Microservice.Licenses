using System.IO.Compression;
using CodeDesignPlus.Net.File.Storage.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.FileStorage;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Google.Protobuf;
using Microsoft.Extensions.Options;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// The email of a paid order and, when the order is invoiced, its electronic invoice (Kappali pendings/661, step 4).
/// </summary>
public interface IPurchaseEmails
{
    /// <summary>
    /// Until when the email of a paid order waits for its electronic invoice: the order is invoiced and the email goes with
    /// the invoice. Null when the order is not invoiced: its email goes when the payment is approved, as before.
    /// </summary>
    /// <param name="order">The paid order.</param>
    /// <returns>The deadline, or null when it does not wait.</returns>
    Instant? WaitsUntil(OrderAggregate order);

    /// <summary>
    /// Sends now the email of the receipt of an order that does not wait for its invoice.
    /// </summary>
    /// <param name="order">The paid order, with its receipt.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task SendReceiptAsync(OrderAggregate order, CancellationToken cancellationToken);

    /// <summary>
    /// Sends what is due of an order whose email waits for its invoice: the receipt with the invoice once it is validated
    /// with its files, the receipt alone when the invoice was rejected or the wait is over, and the invoice alone when it
    /// arrives after the receipt. Nothing else, and never twice.
    /// </summary>
    /// <param name="orderId">The order.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task.</returns>
    Task SendDueAsync(Guid orderId, CancellationToken cancellationToken);
}

/// <summary>
/// What is due of the email of an order: the receipt, the invoice, both or nothing.
/// </summary>
/// <param name="Receipt">Whether the email of the receipt goes.</param>
/// <param name="Invoice">Whether the electronic invoice goes.</param>
public sealed record PurchaseEmailDue(bool Receipt, bool Invoice)
{
    /// <summary>
    /// Nothing is due.
    /// </summary>
    public static readonly PurchaseEmailDue Nothing = new(false, false);

    /// <summary>
    /// What is due of an order at an instant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only an order that waits for its invoice is decided here: one that does not wait sent its email when the payment was
    /// approved, and one paid before the wait existed has no mark of its email (its receipt went out already).
    /// </para>
    /// <para>
    /// The email waits for the invoice, not for a credit note: in a license change the note goes first and the invoice
    /// after it, and what the buyer receives is the invoice.
    /// </para>
    /// </remarks>
    /// <param name="order">The order.</param>
    /// <param name="now">The current instant.</param>
    /// <returns>What is due.</returns>
    public static PurchaseEmailDue Of(OrderAggregate order, Instant now)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.ReceiptEmailWaitsUntil is not { } waitsUntil || order.Receipt is null)
        {
            return Nothing;
        }

        var invoiceReady = order.Invoice?.CanBeDelivered == true;

        if (order.ReceiptEmailSentAt is null)
        {
            if (invoiceReady)
            {
                return new PurchaseEmailDue(true, true);
            }

            // A rejected invoice may take days to be corrected: the receipt does not wait for it.
            return order.Invoice?.State == ElectronicDocumentState.Rejected || now >= waitsUntil ? new PurchaseEmailDue(true, false) : Nothing;
        }

        return invoiceReady && order.InvoiceEmailSentAt is null ? new PurchaseEmailDue(false, true) : Nothing;
    }
}

/// <summary>
/// Sends the email of a paid order through ms-emails and, when the order is invoiced, waits for its electronic invoice to
/// attach it (Kappali pendings/661, step 4).
/// </summary>
/// <remarks>
/// <para>
/// The invoice goes as the tax authority asks it to be delivered to the buyer: its XML and a ZIP with the attached document
/// (the signed XML with its validation) and the graphic representation. ms-emails reads the attachments from the
/// copropiedad's folder of ms-filestorage, and the platform's documents live in the platform's container, so they are read
/// here, packed and saved again in the order's folder (<c>licenses-pdf</c>) with ids derived from the order: a retry saves
/// on the same records.
/// </para>
/// <para>
/// Each email is reserved on the order with a conditional write before it is published, so the consumer of the invoice and
/// the job that ends the wait never send it twice; if the publication fails, the reservation is given back and the message
/// or the next round of the job sends it.
/// </para>
/// </remarks>
/// <param name="orders">The repository of orders.</param>
/// <param name="pubsub">The bus.</param>
/// <param name="currencies">The currencies of ms-locations.</param>
/// <param name="uploads">ms-filestorage, to save the attachments in the order's folder.</param>
/// <param name="storage">The storage, to read the files of the invoice.</param>
/// <param name="options">How the platform invoices.</param>
/// <param name="clock">The clock.</param>
/// <param name="logger">The logger.</param>
public sealed class PurchaseEmails(IOrderRepository orders, IPubSub pubsub, ICurrencyGrpc currencies, IFileStorageGrpc uploads, IFileStorage storage, IOptions<PlatformInvoicingOptions> options, IClock clock, ILogger<PurchaseEmails> logger) : IPurchaseEmails
{
    /// <summary>
    /// The namespace of the ids of the attachments of the invoice saved in the order's folder. A fixed value is what makes a
    /// retry save on the same records; it is not a setting.
    /// </summary>
    private static readonly Guid AttachmentNamespace = Guid.Parse("4c2b8e19-7d36-4a51-9f0e-1b6a3c8d2e47");

    /// <inheritdoc/>
    public Instant? WaitsUntil(OrderAggregate order)
    {
        ArgumentNullException.ThrowIfNull(order);

        var settings = options.Value;

        // The same orders the invoice is asked for: paid, not superseded (pendings/661).
        if (!settings.Enable || order.PaymentStatus != PaymentStatus.Succeeded || order.SupersededBy is not null || order.LicenseApplicationFailed)
        {
            return null;
        }

        return clock.GetCurrentInstant() + Duration.FromTimeSpan(settings.EmailWaitForInvoice);
    }

    /// <inheritdoc/>
    public async Task SendReceiptAsync(OrderAggregate order, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(order);

        if (order.Receipt is null)
        {
            return;
        }

        await SendAsync(order, true, null, clock.GetCurrentInstant(), cancellationToken);
    }

    /// <inheritdoc/>
    public async Task SendDueAsync(Guid orderId, CancellationToken cancellationToken)
    {
        var order = await orders.FindAsync<OrderAggregate>(orderId, cancellationToken);

        if (order is null)
        {
            return;
        }

        var now = clock.GetCurrentInstant();
        var due = PurchaseEmailDue.Of(order, now);

        if (due == PurchaseEmailDue.Nothing)
        {
            return;
        }

        InvoiceAttachments? invoice = null;

        if (due.Invoice)
        {
            try
            {
                invoice = await InvoiceAttachmentsAsync(order, cancellationToken);
            }
            catch (Exception exception) when (due.Receipt && now >= order.ReceiptEmailWaitsUntil && exception is not OperationCanceledException)
            {
                // The wait is over and the invoice cannot be packed: the buyer gets the receipt now, and the invoice in its
                // own email when it can be.
                logger.LogError(exception, "The electronic invoice of order {OrderId} could not be attached; its receipt goes alone.", order.Id);

                due = new PurchaseEmailDue(true, false);
            }
        }

        await SendAsync(order, due.Receipt, invoice, now, cancellationToken);
    }

    private async Task SendAsync(OrderAggregate order, bool receipt, InvoiceAttachments? invoice, Instant now, CancellationToken cancellationToken)
    {
        var claimed = receipt
            ? await orders.ClaimReceiptEmailAsync(order.Id, invoice is not null, now, cancellationToken)
            : await orders.ClaimInvoiceEmailAsync(order.Id, now, cancellationToken);

        if (!claimed)
        {
            logger.LogInformation("The email of order {OrderId} was already sent by another process.", order.Id);

            return;
        }

        try
        {
            var email = receipt ? await ReceiptEmailAsync(order, invoice, cancellationToken) : InvoiceEmail(order, invoice!, now);

            await pubsub.PublishAsync(email, cancellationToken);
        }
        catch
        {
            if (receipt)
            {
                await orders.ReleaseReceiptEmailAsync(order.Id, invoice is not null, now, CancellationToken.None);
            }
            else
            {
                await orders.ReleaseInvoiceEmailAsync(order.Id, now, CancellationToken.None);
            }

            throw;
        }

        logger.LogInformation("The email of order {OrderId} was sent (receipt: {Receipt}, invoice: {Invoice}).", order.Id, receipt, invoice?.Number);
    }

    /// <summary>
    /// The email of the receipt, with the template of the kind of order, as it was sent when the payment was approved, and
    /// the invoice when it goes with it.
    /// </summary>
    private async Task<SendEmailDomainEvent> ReceiptEmailAsync(OrderAggregate order, InvoiceAttachments? invoice, CancellationToken cancellationToken)
    {
        var currency = await currencies.GetCurrencyAsync(code: order.Total.Currency, cancellationToken: cancellationToken);

        var (template, variables) = order.Type switch
        {
            OrderType.Renewal => ("SubscriptionRenewed", RenewalEmailVariables.Build(order, currency)),
            // Un cambio no es una compra: dice lo descontado y lo pagado, y el comprobante va adjunto (pendings/738).
            OrderType.Change => (ChangeEmailVariables.Template, ChangeEmailVariables.Build(order, currency)),
            // Un paquete: lo que se agregó, su vigencia, lo descontado y lo pagado (pendings/739).
            OrderType.Package => (ChangeEmailVariables.PackageTemplate, ChangeEmailVariables.BuildPackage(order, currency)),
            _ => ("PurchaseConfirmation", ReceiptEmailVariables.Build(order, currency))
        };

        variables[ElectronicInvoiceText.NoteVariable] = invoice is null ? string.Empty : ElectronicInvoiceText.AttachedNote(invoice.Number);

        List<FileAttachment> attachments = [order.Receipt!, .. invoice?.Files ?? []];

        return new SendEmailDomainEvent(Guid.NewGuid(), template, Recipients(order), [], [], variables, attachments, order.TenantDetail.Id);
    }

    private static SendEmailDomainEvent InvoiceEmail(OrderAggregate order, InvoiceAttachments invoice, Instant now)
    {
        var variables = new Dictionary<string, string>
        {
            ["organization_name"] = order.TenantDetail.Name,
            ["license_name"] = order.License.Name,
            ["invoice_number"] = invoice.Number,
            [ElectronicInvoiceText.NoteVariable] = ElectronicInvoiceText.AttachedNote(invoice.Number),
            ["current_year"] = now.InUtc().Year.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };

        return new SendEmailDomainEvent(Guid.NewGuid(), ElectronicInvoiceText.InvoiceTemplate, Recipients(order), [], [], variables, [.. invoice.Files], order.TenantDetail.Id);
    }

    /// <summary>
    /// The XML of the invoice and the ZIP with its attached document and its graphic representation, saved in the order's
    /// folder of ms-filestorage.
    /// </summary>
    private async Task<InvoiceAttachments> InvoiceAttachmentsAsync(OrderAggregate order, CancellationToken cancellationToken)
    {
        var invoice = order.Invoice!;
        var number = invoice.Number ?? throw new InvalidOperationException($"The invoice of order {order.Id} has no number.");

        var xml = await ReadAsync(invoice.Xml!, cancellationToken);
        var attached = await ReadAsync(invoice.AttachedDocument!, cancellationToken);
        var pdf = await ReadAsync(invoice.Pdf!, cancellationToken);

        var zip = Pack((ElectronicInvoiceText.AttachedDocumentEntry(number), attached), (ElectronicInvoiceText.PdfEntry(number), pdf));

        var savedXml = await SaveAsync(order, "invoice-xml", ElectronicInvoiceText.XmlFileName(number), xml, cancellationToken);
        var savedZip = await SaveAsync(order, "invoice-zip", ElectronicInvoiceText.ZipFileName(number), zip, cancellationToken);

        return new InvoiceAttachments(number, [savedXml, savedZip]);
    }

    private async Task<byte[]> ReadAsync(FileAttachment file, CancellationToken cancellationToken)
    {
        // The platform's files live in {target}/{id}/{name} of the platform's container of ms-filestorage (rule 56).
        var response = await storage.DownloadAsync(file.Name, $"{file.Target}/{file.Id}", options.Value.FilesContainer, cancellationToken);

        if (response is not { Success: true, Stream: not null })
        {
            throw new InvalidOperationException($"The file {file.Target}/{file.Id}/{file.Name} of the electronic invoice could not be read.");
        }

        using var buffer = new MemoryStream();

        await response.Stream.CopyToAsync(buffer, cancellationToken);

        return buffer.ToArray();
    }

    private async Task<FileAttachment> SaveAsync(OrderAggregate order, string kind, string fileName, byte[] content, CancellationToken cancellationToken)
    {
        var id = DeterministicGuid.Create(AttachmentNamespace, $"{order.Id}:{kind}");

        var stored = await uploads.UploadAsync(new UploadFileRequest
        {
            Id = id.ToString(),
            Tenant = order.TenantDetail.Id.ToString(),
            UploadedBy = order.Buyer.BuyerId.ToString(),
            Target = Commands.UpdateStateOrder.UpdateStateOrderCommandHandler.ReceiptTarget,
            FileName = fileName,
            Content = ByteString.CopyFrom(content)
        }, cancellationToken);

        return new FileAttachment(Guid.Parse(stored.Id), string.IsNullOrWhiteSpace(stored.FileName) ? fileName : stored.FileName, stored.Target);
    }

    /// <summary>
    /// Packs the files in a ZIP, in memory.
    /// </summary>
    /// <param name="entries">The name and the content of each file.</param>
    /// <returns>The ZIP.</returns>
    public static byte[] Pack(params (string Name, byte[] Content)[] entries)
    {
        using var buffer = new MemoryStream();

        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (name, content) in entries)
            {
                var entry = archive.CreateEntry(name, CompressionLevel.Optimal);

                using var stream = entry.Open();

                stream.Write(content);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// A quién va el correo: a quien pagó y, en una renovación o un cambio, también al correo de la copropiedad.
    /// </summary>
    private static List<string> Recipients(OrderAggregate order) => order.Type == OrderType.Purchase
        ? [order.Buyer.Email]
        : [.. new[] { order.Buyer.Email, order.TenantDetail.Email }.Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase)];

    private sealed record InvoiceAttachments(string Number, List<FileAttachment> Files);
}
