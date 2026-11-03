using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.ValueObjects.Financial;
using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Represents the pricing configuration for a license.
/// Handles base price, discounts, taxes, and automatically calculates exact subtotals and totals.
/// </summary>
public sealed partial record Price
{
    /// <summary>
    /// The billing type for the license.
    /// </summary>
    public BillingType BillingType { get; private set; }
    /// <summary>
    /// The billing model for the license.
    /// </summary>
    public BillingModel BillingModel { get; private set; }

    /// <summary>
    /// The base price of the license. Encapsulates both the amount and the currency.
    /// </summary>
    public Money BasePrice { get; private set; } 

    /// <summary>
    /// The discount rate in basis points (e.g., 1550 for 15.50%).
    /// </summary>
    public int DiscountBasisPoints { get; private set; }

    /// <summary>
    /// The tax rate in basis points (e.g., 1900 for 19.00%).
    /// </summary>
    public int TaxBasisPoints { get; private set; }

    // Los dos miembros que siguen no van al constructor (regla 31): los precios guardados antes de existir no los traen y
    // se leen como gravados, que es lo que eran todos (pendings/662).

    /// <summary>
    /// Cómo trata el impuesto este precio: gravado con <see cref="TaxBasisPoints"/>, excluido o exento (pendings/662).
    /// </summary>
    [JsonProperty]
    public TaxTreatment TaxTreatment { get; private set; }

    /// <summary>
    /// La norma que sostiene un precio excluido o exento, tal como la escribe quien configura la licencia. Vacía en un
    /// precio gravado.
    /// </summary>
    /// <remarks>
    /// Es texto y no un catálogo de normas: el micro es de CodeDesignPlus y no conoce el estatuto tributario de ningún país.
    /// </remarks>
    [JsonProperty]
    public string? TaxLegalBasis { get; private set; }

    /// <summary>
    /// The discount amount calculated based on the base price and discount rate.
    /// </summary>
    public Money Discount => BasePrice * (DiscountBasisPoints / 10000m);

    /// <summary>
    /// The subtotal amount after applying the discount.
    /// </summary>
    public Money SubTotal => BasePrice - Discount;

    /// <summary>
    /// The tax amount calculated based on the subtotal and tax rate.
    /// </summary>
    public Money Tax => SubTotal * (TaxBasisPoints / 10000m);
    
    /// <summary>
    /// The total amount after applying the discount and adding the tax.
    /// </summary>
    public Money Total => SubTotal + Tax;


    /// <summary>
    /// El 100 % en puntos basicos: ni el descuento ni el impuesto pueden pasar de ahi (plan 063 de pendings/).
    /// </summary>
    public const int MaxBasisPoints = 10000;

    [JsonConstructor]
    private Price(BillingType billingType, Money basePrice, BillingModel billingModel, int discountBasisPoints, int taxBasisPoints)
    {
        DomainGuard.IsNull(basePrice, Errors.PriceLicenseIsRequired);
        DomainGuard.IsLessThan(discountBasisPoints, 0, Errors.DiscountLicenseCannotBeLessThanZero);
        DomainGuard.IsLessThan(taxBasisPoints, 0, Errors.TaxLicenseCannotBeLessThanZero);
        DomainGuard.IsGreaterThan(discountBasisPoints, MaxBasisPoints, Errors.DiscountLicenseCannotExceedOneHundredPercent);
        DomainGuard.IsGreaterThan(taxBasisPoints, MaxBasisPoints, Errors.TaxLicenseCannotExceedOneHundredPercent);

        if (billingModel != BillingModel.None)
            DomainGuard.IsLessThan(basePrice.Amount, 0m, Errors.PriceLicenseCannotBeLessThanZero);

        this.BillingType = billingType;
        this.BasePrice = basePrice;
        this.BillingModel = billingModel;
        this.DiscountBasisPoints = discountBasisPoints;
        this.TaxBasisPoints = taxBasisPoints;
    }
    
    /// <summary>
    /// Creates a new instance of the <see cref="Price"/> class.
    /// </summary>
    /// <remarks>
    /// Un precio gravado lleva una tarifa mayor que cero y ninguna base legal; uno excluido o exento, tarifa cero y su base
    /// legal (pendings/662). La tarifa no tiene valor por defecto: la decide quien configura la licencia.
    /// </remarks>
    /// <param name="billingType">The billing type.</param>
    /// <param name="basePrice">The base price.</param>
    /// <param name="billingModel">The billing model.</param>
    /// <param name="discountBasisPoints">The discount rate in basis points (e.g., 1550 for 15.50%).</param>
    /// <param name="taxTreatment">Whether the price is taxed, excluded or exempt.</param>
    /// <param name="taxBasisPoints">The tax rate in basis points (e.g., 1900 for 19.00%); zero unless taxed.</param>
    /// <param name="taxLegalBasis">The legal basis of an excluded or exempt price; empty when taxed.</param>
    /// <returns>A new instance of the <see cref="Price"/> class.</returns>
    public static Price Create(BillingType billingType, Money basePrice, BillingModel billingModel, int discountBasisPoints, TaxTreatment taxTreatment, int taxBasisPoints, string? taxLegalBasis)
    {
        DomainGuard.IsFalse(Enum.IsDefined(taxTreatment), Errors.TaxTreatmentIsInvalid);

        var hasLegalBasis = !string.IsNullOrWhiteSpace(taxLegalBasis);

        if (taxTreatment == TaxTreatment.Taxed)
        {
            DomainGuard.IsTrue(taxBasisPoints <= 0, Errors.TaxedPriceRequiresARate);
            DomainGuard.IsTrue(hasLegalBasis, Errors.TaxedPriceCannotCarryALegalBasis);
        }
        else
        {
            DomainGuard.IsTrue(taxBasisPoints != 0, Errors.UntaxedPriceCannotCarryARate);
            DomainGuard.IsFalse(hasLegalBasis, Errors.UntaxedPriceRequiresALegalBasis);
        }

        return new Price(billingType, basePrice, billingModel, discountBasisPoints, taxBasisPoints)
        {
            TaxTreatment = taxTreatment,
            TaxLegalBasis = hasLegalBasis ? taxLegalBasis!.Trim() : null
        };
    }
}