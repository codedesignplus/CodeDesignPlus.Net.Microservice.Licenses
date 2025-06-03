using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Price
{
    public BillingTypeEnum BillingType { get; private set; }

    public Currency Currency { get; private set; } = null!;
    public long Pricing { get; private set; }
    public short Discount { get; set; } = 0;

    public BillingModel BillingModel { get; set; }

    [JsonConstructor]
    private Price(BillingTypeEnum billingType, Currency currency, long pricing, BillingModel billingModel, short discount)
    {
        DomainGuard.IsNull(currency, Errors.CurrencyLicenseIsRequired);
        DomainGuard.IsLessThan(discount, 0, Errors.DiscountLicenseCannotBeLessThanZero);

        if (billingModel != BillingModel.None)
            DomainGuard.IsLessThan(pricing, 0, Errors.PriceLicenseCannotBeLessThanZero);

        this.BillingType = billingType;
        this.Currency = currency;
        this.Pricing = pricing;
        this.BillingModel = billingModel;
        this.Discount = discount;
    }
    public static Price Create(BillingTypeEnum billingType, Currency currency, long pricing, BillingModel billingModel, short discount)
    {
        return new Price(billingType, currency, pricing, billingModel, discount);
    }
}
