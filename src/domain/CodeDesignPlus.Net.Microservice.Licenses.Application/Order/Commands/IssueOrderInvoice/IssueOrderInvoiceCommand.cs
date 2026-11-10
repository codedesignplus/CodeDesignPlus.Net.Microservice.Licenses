namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.IssueOrderInvoice;

/// <summary>
/// Asks ms-electronicinvoicing for the electronic documents of a paid order (pendings/661): the credit notes on the
/// invoices whose unused period it credits, and then its own invoice.
/// </summary>
/// <param name="OrderId">The paid order.</param>
public record IssueOrderInvoiceCommand(Guid OrderId) : IRequest;

/// <summary>
/// Validates <see cref="IssueOrderInvoiceCommand"/>.
/// </summary>
public class Validator : AbstractValidator<IssueOrderInvoiceCommand>
{
    /// <summary>
    /// Creates the rules.
    /// </summary>
    public Validator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
