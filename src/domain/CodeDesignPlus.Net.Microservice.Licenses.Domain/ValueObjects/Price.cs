using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Price
{
    public BillingTypeEnum BillingType { get; private set; }

    public Currency Currency { get; private set; } = null!;
    public long Pricing { get; private set; }
    public ushort DiscountPercentage { get; private set; }
    public ushort TaxPercentage { get; private set; }
    
    public long SubTotal
    {
        get => Pricing - (Pricing * (DiscountPercentage / 100));
    }

    public long Tax
    {
        get => SubTotal * (TaxPercentage / 100);
    }
    
    public long Total
    {
        get => SubTotal + Tax;
    }

    public BillingModel BillingModel { get; set; }

    [JsonConstructor]
    private Price(BillingTypeEnum billingType, Currency currency, long pricing, BillingModel billingModel, ushort discountPercentage, ushort taxPercentage)
    {
        DomainGuard.IsNull(currency, Errors.CurrencyLicenseIsRequired);
        DomainGuard.IsLessThan(discountPercentage, 0, Errors.DiscountLicenseCannotBeLessThanZero);

        if (billingModel != BillingModel.None)
            DomainGuard.IsLessThan(pricing, 0, Errors.PriceLicenseCannotBeLessThanZero);

        this.BillingType = billingType;
        this.Currency = currency;
        this.Pricing = pricing;
        this.BillingModel = billingModel;
        this.DiscountPercentage = discountPercentage;
        this.TaxPercentage = taxPercentage;
    }
    public static Price Create(BillingTypeEnum billingType, Currency currency, long pricing, BillingModel billingModel, ushort discountPercentage, ushort taxPercentage)
    {
        return new Price(billingType, currency, pricing, billingModel, discountPercentage, taxPercentage);
    }
}
