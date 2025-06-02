using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Price
{
    public BillingTypeEnum BillingType { get; private set; }

    public Currency Currency { get; private set; } = null!;

    public long Pricing { get; private set; }

    public BillingModel BillingModel { get; set; }

    [JsonConstructor]
    private Price(BillingTypeEnum billingType, Currency currency, long pricing, BillingModel billingModel)
    {
        DomainGuard.IsNull(currency, Errors.CurrencyLicenseIsRequired);

        if (billingModel != BillingModel.None)
            DomainGuard.IsLessThan(pricing, 0, Errors.PriceLicenseCannotBeLessThanZero);

        this.BillingType = billingType;
        this.Currency = currency;
        this.Pricing = pricing;
        this.BillingModel = billingModel;
    }
    public static Price Create(BillingTypeEnum billingType, Currency currency, long pricing, BillingModel billingModel)
    {
        return new Price(billingType, currency, pricing, billingModel);
    }
}
