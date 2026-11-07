namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetAnyOrderReceiptUrl;

/// <summary>
/// A freshly signed URL of the receipt of any order, whatever its tenant: the platform's view, like
/// <c>GET Order/all/{id}</c> (pendings/674).
/// </summary>
/// <param name="OrderId">The order.</param>
public record GetAnyOrderReceiptUrlQuery(Guid OrderId) : IRequest<string?>;

/// <summary>
/// Validator of <see cref="GetAnyOrderReceiptUrlQuery"/>.
/// </summary>
public class Validator : AbstractValidator<GetAnyOrderReceiptUrlQuery>
{
    /// <summary>
    /// Initializes the rules of the query.
    /// </summary>
    public Validator()
    {
        RuleFor(x => x.OrderId).NotEmpty();
    }
}
