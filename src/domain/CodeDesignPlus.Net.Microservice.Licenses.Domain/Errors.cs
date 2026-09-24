using CodeDesignPlus.Net.Exceptions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

public class Errors : IErrorCodes
{
    public static readonly Error IdLicenseIsRequired = new("101");
    public static readonly Error NameLicenseIsRequired = new("102");
    public static readonly Error DescriptionLicenseIsRequired = new("103");
    public static readonly Error CurrencyLicenseIsRequired = new("104");
    public static readonly Error PriceLicenseCannotBeLessThanZero = new("105");
    public static readonly Error CreatedByLicenseIsRequired = new("106");
    public static readonly Error NameCurrencyIsRequired = new("107");
    public static readonly Error CodeCurrencyIsRequired = new("108");
    public static readonly Error SymbolCurrencyIsRequired = new("109");
    public static readonly Error IdModuleIsRequired = new("110");
    public static readonly Error ModuleAlreadyExists = new("111");
    public static readonly Error ModuleNotFound = new("112");
    public static readonly Error PriceLicenseIsRequired = new("113");
    public static readonly Error DescriptionModuleIsRequired = new("114");
    public static readonly Error IconLicenseIsRequired = new("115");
    public static readonly Error ShortDescriptionLicenseIsRequired = new("116");
    public static readonly Error DiscountLicenseCannotBeLessThanZero = new("117");
    public static readonly Error ColorLicenseIsRequired = new("118");
    public static readonly Error NameIsRequired = new("119");
    public static readonly Error NameIsTooLong = new("120");
    public static readonly Error PhoneIsRequired = new("121");
    public static readonly Error PhoneContainsInvalidCharacters = new("122");
    public static readonly Error EmailIsRequired = new("123");
    public static readonly Error EmailContainsInvalidCharacters = new("124");
    public static readonly Error TypeDocumentIsRequired = new("125");
    public static readonly Error DocumentIsRequired = new("126");
    public static readonly Error DocumentIsTooLong = new("127");
    public static readonly Error AddressIsRequired = new("128");
    public static readonly Error PostalCodeIsRequired = new("129");
    public static readonly Error CityNameIsEmpty = new("130");
    public static readonly Error CityIdIsEmpty = new("131");
    public static readonly Error WebContainsInvalidCharacters = new("132");
    public static readonly Error LocationIsRequired = new("133");
    public static readonly Error CurrencyIdIsEmpty = new("134");
    public static readonly Error CountryNameIsEmpty = new("135");
    public static readonly Error CountryIdIsEmpty = new("136");
    public static readonly Error CountryCodeIsInvalid = new("137");
    public static readonly Error CountryTimeZoneIsEmpty = new("138");
    public static readonly Error LocalityNameIsEmpty = new("139");
    public static readonly Error LocalityIdIsEmpty = new("140");
    public static readonly Error NeighborhoodNameIsEmpty = new("141");
    public static readonly Error NeighborhoodIdIsEmpty = new("142");
    public static readonly Error StateNameIsEmpty = new("143");
    public static readonly Error StateIdIsEmpty = new("144");
    public static readonly Error StateCodeIsEmpty = new("145");
    public static readonly Error CountryIsNull = new("146");
    public static readonly Error StateIsNull = new("147");
    public static readonly Error CityIsNull = new("148");
    public static readonly Error LocalityIsNull = new("149");
    public static readonly Error NeighborhoodIsNull = new("150");
    public static readonly Error PseCodeCannotBeNullOrEmpty = new("151");
    public static readonly Error PseCodeCannotBeGreaterThan34Characters = new("152");
    public static readonly Error TypePersonCannotBeNullOrEmpty = new("153");
    public static readonly Error TypePersonCannotBeGreaterThan2Characters = new("154");
    public static readonly Error PseCannotBeNull = new("155");
    public static readonly Error CreditCardNumberCannotBeNullOrEmpty = new("156");
    public static readonly Error CreditCardNumberCannotBeGreaterThan20Characters = new("157"); 
    public static readonly Error CreditCardNumberCannotBeLessThan13Characters = new("158"); 
    public static readonly Error CreditCardSecurityCodeCannotBeNullOrEmpty = new("159"); 
    public static readonly Error CreditCardSecurityCodeCannotBeGreaterThan4Characters = new("160"); 
    public static readonly Error CreditCardSecurityCodeCannotBeLessThan3Characters = new("161"); 
    public static readonly Error CreditCardExpirationDateCannotBeNullOrEmpty = new("162"); 
    public static readonly Error CreditCardExpirationDateMustBeValidFormat = new("163");
    public static readonly Error CreditCardExpirationDateCannotBeGreaterThan7Characters = new("164");
    public static readonly Error CreditCardCannotBeNull = new("165");
    public static readonly Error PaymentMethodIsRequired = new("166"); 
    public static readonly Error BuyerIsRequired = new("167"); 
    public static readonly Error TenantDetailIsRequired = new("168");
    public static readonly Error CardHolderNameCannotBeNullOrEmpty = new("169");
    public static readonly Error IdOrderIsRequired = new("170");
    public static readonly Error LicenseIdIsRequired = new("171");
    public static readonly Error IdTenantIsRequired = new("172");
    public static readonly Error CountryAlpha2IsEmpty = new("173");
    public static readonly Error CodeOfThePaymentMethodIsRequired = new("174");
    public static readonly Error PseResponseUrlCannotBeNullOrEmpty = new("175"); 
    public static readonly Error PseResponseUrlCannotBeGreaterThan200Characters = new("176");
    public static readonly Error PseResponseUrlMustBeValidFormat = new("177");
    public static readonly Error CodeTypeDocumentCannotBeNullOrEmpty = new("178");
    public static readonly Error NameTypeDocumentIsInvalid = new("179");
    public static readonly Error CodeTypeDocumentIsInvalid = new("180");
    public static readonly Error NumberDocumentIsRequired = new("181");
    public static readonly Error NameOfLicenseIsRequired = new("182"); 
    public static readonly Error TotalOfLicenseShouldBeGreaterThanZero = new("183");
    public static readonly Error TaxOfLicenseShouldBeGreaterThanZero = new("184"); 
    public static readonly Error SubTotalOfLicenseShouldBeGreaterThanZero = new("185");
    public static readonly Error TotalIsNotEqualToTaxAndSubTotal = new("186");
    public static readonly Error IdOfLicenseIsRequired = new("187");
    public static readonly Error LicenseIdIsEmpty = new("188");
    public static readonly Error LicenseNameIsEmpty = new("189");
    public static readonly Error LicenseStartDateGreaterThanEndDate = new("190");
    public static readonly Error LicenseMetadataIsNull = new("191");    
    public static readonly Error LicenseNameIsInvalid = new("192");
    public static readonly Error IdBuyerIsRequired = new("193");
    public static readonly Error IdPaymentIsRequired = new("194");

    public static readonly Error TaxLicenseCannotBeLessThanZero = new("195");

    public static readonly Error DuplicatePricingStrategyFound = new("196");

    public static readonly Error TotalOfLicenseIsRequired = new("197");
    public static readonly Error TaxOfLicenseIsRequired = new("198");
    public static readonly Error SubTotalOfLicenseIsRequired = new("199");
    public static readonly Error CurrenciesMustMatch = new("216");

    public static readonly Error ModulesLicenseIsRequired = new("218");
    public static readonly Error AttributesLicenseIsRequired = new("219");
    public static readonly Error OrderIsNotSucceeded = new("224");

    public static readonly Error DescriptionOfLicenseIsRequired = new("225");
    public static readonly Error ShortDescriptionOfLicenseIsRequired = new("215");
    public static readonly Error TermsOfServiceOfLicenseIsRequired = new("217");
    public static readonly Error NameModuleIsRequired = new("220");

    public static readonly Error FileAttachmentIdIsInvalid = new("221");
    public static readonly Error FileAttachmentNameIsInvalid = new("222");
    public static readonly Error FileAttachmentTargetIsInvalid = new("223");
}

