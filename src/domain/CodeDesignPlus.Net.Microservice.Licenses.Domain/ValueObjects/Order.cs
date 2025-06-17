using System.Text.Json.Serialization;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class OrderDetail
{
    [GeneratedRegex(@"^https?:\/\/([a-zA-Z0-9\-\.]+)(:[0-9]+)?(\/[^\s]*)?$", RegexOptions.Compiled)]
    private static partial Regex UrlRegex();
    public Guid Id { get; private set; }
    public BillingTypeEnum BillingType { get; private set; }
    public BillingModel BillingModel { get; private set; } 
    public long Total { get; private set; } = 0;
    public long Tax { get; private set; } = 0;
    public long SubTotal { get; private set; } = 0;
    public Buyer Buyer { get; private set; } = null!;
    public string NotifyUrl { get; private set; } = null!;

    [JsonConstructor]
    public OrderDetail(Guid id, long total, long tax, long subTotal, Buyer buyer, BillingTypeEnum billingType, BillingModel billingModel, string notifyUrl)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdOrderIsRequired);
        DomainGuard.IsLessThan(total, 1, Errors.TotalIsInvalid);
        DomainGuard.IsLessThan(tax, 0, Errors.TaxIsInvalid);
        DomainGuard.IsLessThan(subTotal, 0, Errors.SubTotalIsInvalid);
        DomainGuard.IsNull(buyer, Errors.BuyerIsRequired);
        DomainGuard.IsNullOrEmpty(notifyUrl, Errors.NotifyUrlIsRequired);
        DomainGuard.IsFalse(UrlRegex().IsMatch(notifyUrl), Errors.NotifyUrlMustBeValidFormat);

        this.Id = id;
        this.Total = total;
        this.Tax = tax;
        this.SubTotal = subTotal;
        this.Buyer = buyer;
        this.BillingType = billingType;
        this.BillingModel = billingModel;
        this.NotifyUrl = notifyUrl;
    }

    public static OrderDetail Create(Guid id, long amount, long tax, long taxReturnBase, Buyer buyer, BillingTypeEnum billingType, BillingModel billingModel, string notifyUrl)
    {
        return new OrderDetail(id, amount, tax, taxReturnBase, buyer, billingType, billingModel, notifyUrl);
    }
}
