using System.Text.Json.Serialization;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class License
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public long Total { get; private set; }
    public long Tax { get; private set; }
    public long SubTotal { get; private set; }
    public BillingTypeEnum BillingType { get; private set; }
    public BillingModel BillingModel { get; private set; } 
    public string Currency { get; private set; } 

    [JsonConstructor]
    public License(Guid id, string name, long total, long tax, long subTotal, BillingTypeEnum billingType, BillingModel billingModel, string currency)
    {
        DomainGuard.GuidIsEmpty(id, Errors.IdOfLicenseIsRequired);
        DomainGuard.IsNullOrEmpty(name, Errors.NameOfLicenseIsRequired);
        DomainGuard.IsLessThan(total, 0, Errors.TotalOfLicenseShouldBeGreaterThanZero);
        DomainGuard.IsLessThan(tax, 0, Errors.TaxOfLicenseShouldBeGreaterThanZero);
        DomainGuard.IsLessThan(subTotal, 0, Errors.SubTotalOfLicenseShouldBeGreaterThanZero);
        DomainGuard.IsFalse(total < tax + subTotal, Errors.TotalIsNotEqualToTaxAndSubTotal);

        this.Id = id;
        this.Name = name;
        this.Total = total;
        this.Tax = tax;
        this.SubTotal = subTotal;
        this.BillingType = billingType;
        this.BillingModel = billingModel;
        this.Currency = currency;
    }

    public static License Create(Guid id, string name, long total, long tax, long subTotal, BillingTypeEnum billingType, BillingModel billingModel, string currency)
    {
        return new License(id, name, total, tax, subTotal, billingType, billingModel, currency);
    }
}
