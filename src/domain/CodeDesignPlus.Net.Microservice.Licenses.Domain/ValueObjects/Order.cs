using System.Text.Json.Serialization;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Order
{
    public Guid Id { get; private set; }
    public long Amount { get; private set; } = 0;
    public long Tax { get; private set; } = 0;
    public long TaxReturnBase { get; private set; } = 0;
    public Buyer Buyer { get; private set; } = null!;

    [JsonConstructor]
    public Order(Guid id, long amount, long tax, long taxReturnBase, Buyer buyer)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdOrderIsRequired);
        DomainGuard.IsLessThan(amount, 1, Errors.AmountIsInvalid);
        DomainGuard.IsLessThan(tax, 0, Errors.TaxIsInvalid);
        DomainGuard.IsLessThan(taxReturnBase, 0, Errors.TaxReturnBaseIsInvalid);
        DomainGuard.IsNull(buyer, Errors.BuyerIsRequired);

        this.Id = id;
        this.Amount = amount;
        this.Tax = tax;
        this.TaxReturnBase = taxReturnBase;
        this.Buyer = buyer;
    }

    public static Order Create(Guid id, long amount, long tax, long taxReturnBase, Buyer buyer)
    {
        return new Order(id, amount, tax, taxReturnBase, buyer);
    }
}
