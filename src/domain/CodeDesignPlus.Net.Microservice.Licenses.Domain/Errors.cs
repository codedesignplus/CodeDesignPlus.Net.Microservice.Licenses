using CodeDesignPlus.Net.Exceptions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

public class Errors : IErrorCodes
{
    public static readonly Error IdLicenseIsRequired = new("101", "The Id of the license is required.");
    public static readonly Error NameLicenseIsRequired = new("102", "The Name of the license is required.");
    public static readonly Error DescriptionLicenseIsRequired = new("103", "The Description of the license is required.");
    public static readonly Error CurrencyLicenseIsRequired = new("104", "The Currency of the license is required.");
    public static readonly Error PriceLicenseCannotBeLessThanZero = new("105", "The Price of the license cannot be less than zero.");
    public static readonly Error CreatedByLicenseIsRequired = new("106", "The CreatedBy of the license is required.");
    public static readonly Error NameCurrencyIsRequired = new("107", "The Name of the currency is required.");
    public static readonly Error CodeCurrencyIsRequired = new("108", "The Code of the currency is required.");
    public static readonly Error SymbolCurrencyIsRequired = new("109", "The Symbol of the currency is required.");
    public static readonly Error IdModuleIsRequired = new("110", "The Id of the module is required.");
    public static readonly Error ModuleAlreadyExists = new("111", "The module already exists in the license.");
    public static readonly Error ModuleNotFound = new("112", "The module was not found.");
    public static readonly Error PriceLicenseIsRequired = new("113", "The Price of the license is required.");
    public static readonly Error DescriptionModuleIsRequired = new("114", "The Description of the module is required.");
    public static readonly Error IconLicenseIsRequired = new("115", "The Icon of the license is required.");
    public static readonly Error ShortDescriptionLicenseIsRequired = new("116", "The description of license is required.");
    public static readonly Error DiscountLicenseCannotBeLessThanZero = new("117", "The Discount of the license cannot be less than zero.");
    public static readonly Error ColorLicenseIsRequired = new("118", "The Color of the license is required.");
    public static readonly Error NameIsRequired = new("119", "The Name is required.");
    public static readonly Error NameIsTooLong = new("120", "The Name is too long, maximum length is 124 characters.");
    public static readonly Error PhoneIsRequired = new("121", "The Phone is required.");
    public static readonly Error PhoneContainsInvalidCharacters = new("122", "The Phone contains invalid characters, it should be a valid phone number format.");
    public static readonly Error EmailIsRequired = new("123", "The Email is required.");
    public static readonly Error EmailContainsInvalidCharacters = new("124", "The Email contains invalid characters, it should be a valid email format.");
    public static readonly Error TypeDocumentIsRequired = new("125", "The TypeDocument is required.");
    public static readonly Error DocumentIsRequired = new("126", "The Document is required.");
    public static readonly Error DocumentIsTooLong = new("127", "The Document is too long, maximum length is 20 characters.");
    public static readonly Error AddressIsRequired = new("128", "The Address is required.");
    public static readonly Error PostalCodeIsRequired = new("129", "The PostalCode is required.");
    public static readonly Error CityNameIsEmpty = new("130", "The city name is invalid.");
    public static readonly Error CityIdIsEmpty = new("131", "The city id is invalid.");
    public static readonly Error WebContainsInvalidCharacters = new("132", "The Web contains invalid characters, it should be a valid URL format.");
    public static readonly Error LocationIsRequired = new("133", "The Location is required.");
    public static readonly Error CurrencyIdIsEmpty = new("134", "The Currency Id is empty.");
    public static readonly Error CountryNameIsEmpty = new("135", "The country name is invalid.");
    public static readonly Error CountryIdIsEmpty = new("136", "The country id is invalid.");
    public static readonly Error CountryCodeIsInvalid = new("137", "The country code is invalid.");
    public static readonly Error CountryTimeZoneIsEmpty = new("138", "The country timeZone is invalid.");
    public static readonly Error LocalityNameIsEmpty = new("139", "The locality name is invalid.");
    public static readonly Error LocalityIdIsEmpty = new("140", "The locality id is invalid.");
    public static readonly Error NeighborhoodNameIsEmpty = new("141", "The neighborhood name is invalid.");
    public static readonly Error NeighborhoodIdIsEmpty = new("142", "The neighborhood id is invalid.");
    public static readonly Error StateNameIsEmpty = new("143", "The state name is invalid.");
    public static readonly Error StateIdIsEmpty = new("144", "The state id is invalid.");
    public static readonly Error StateCodeIsEmpty = new("145", "The state code is invalid.");
    public static readonly Error CountryIsNull = new("146", "The country is null.");
    public static readonly Error StateIsNull = new("147", "The state is null.");
    public static readonly Error CityIsNull = new("148", "The city is null.");
    public static readonly Error LocalityIsNull = new("149", "The locality is null.");
    public static readonly Error NeighborhoodIsNull = new("150", "The neighborhood is null.");
    public static readonly Error PseCodeCannotBeNullOrEmpty = new("151", "Pse Code cannot be null or empty");
    public static readonly Error PseCodeCannotBeGreaterThan34Characters = new("152", "Pse Code cannot be greater than 34 characters");
    public static readonly Error TypePersonCannotBeNullOrEmpty = new("153", "Type Person cannot be null or empty");
    public static readonly Error TypePersonCannotBeGreaterThan2Characters = new("154", "Type Person cannot be greater than 2 characters");
    public static readonly Error PseCannotBeNull = new("155", "Pse cannot be null");
    public static readonly Error CreditCardNumberCannotBeNullOrEmpty = new("156", "Credit Card Number cannot be null or empty");
    public static readonly Error CreditCardNumberCannotBeGreaterThan20Characters = new("157", "Credit Card Number cannot be greater than 20 characters"); 
    public static readonly Error CreditCardNumberCannotBeLessThan13Characters = new("158", "Credit Card Number cannot be less than 13 characters"); 
    public static readonly Error CreditCardSecurityCodeCannotBeNullOrEmpty = new("159", "Credit Card Security Code cannot be null or empty"); 
    public static readonly Error CreditCardSecurityCodeCannotBeGreaterThan4Characters = new("160", "Credit Card Security Code cannot be greater than 4 characters"); 
    public static readonly Error CreditCardSecurityCodeCannotBeLessThan3Characters = new("161", "Credit Card Security Code cannot be less than 3 characters"); 
    public static readonly Error CreditCardExpirationDateCannotBeNullOrEmpty = new("162", "Credit Card Expiration Date cannot be null or empty"); 
    public static readonly Error CreditCardExpirationDateMustBeValidFormat = new("163", "Credit Card Expiration Date must be valid format");
    public static readonly Error CreditCardExpirationDateCannotBeGreaterThan7Characters = new("164", "Credit Card Expiration Date cannot be greater than 7 characters");
    public static readonly Error CreditCardCannotBeNull = new("165", "Credit Card cannot be null");
    public static readonly Error PaymentMethodIsRequired = new("166", "The Payment Method is required."); 
    public static readonly Error BuyerIsRequired = new("167", "The Buyer is required."); 
    public static readonly Error TenantDetailIsRequired = new("168", "The Tenant Detail is required.");
    public static readonly Error CardHolderNameCannotBeNullOrEmpty = new("169", "Card Holder Name cannot be null or empty");
    public static readonly Error IdOrderIsRequired = new("170", "The Id Order is required.");
    public static readonly Error LicenseIdIsRequired = new("171", "The License Id is required.");
    public static readonly Error IdTenantIsRequired = new("172", "The Id Tenant is required.");
    public static readonly Error CountryAlpha2IsEmpty = new("173", "The Country Alpha2 is empty.");
    public static readonly Error CodeOfThePaymentMethodIsRequired = new("174", "The code of the payment method is required.");
    public static readonly Error PseResponseUrlCannotBeNullOrEmpty = new("175", "The Pse Response URL cannot be null or empty."); 
    public static readonly Error PseResponseUrlCannotBeGreaterThan200Characters = new("176", "The Pse Response URL cannot be greater than 200 characters.");
    public static readonly Error PseResponseUrlMustBeValidFormat = new("177", "The Pse Response URL must be a valid URL.");
    public static readonly Error CodeTypeDocumentCannotBeNullOrEmpty = new("178", "The Code Type Document cannot be null or empty.");
    public static readonly Error NameTypeDocumentIsInvalid = new("179", "The Name Type Document is invalid.");
    public static readonly Error CodeTypeDocumentIsInvalid = new("180", "The Code Type Document is invalid.");
    public static readonly Error NumberDocumentIsRequired = new("181", "The Number Document is required.");
    public static readonly Error NameOfLicenseIsRequired = new("182", "The Name of the license is required."); 
    public static readonly Error TotalOfLicenseShouldBeGreaterThanZero = new("183", "The Total of the license should be greater than zero.");
    public static readonly Error TaxOfLicenseShouldBeGreaterThanZero = new("184", "The Tax of the license should be greater than zero."); 
    public static readonly Error SubTotalOfLicenseShouldBeGreaterThanZero = new("185", "The SubTotal of the license should be greater than zero.");
    public static readonly Error TotalIsNotEqualToTaxAndSubTotal = new("186", "The Total is not equal to the sum of Tax and SubTotal.");
    public static readonly Error IdOfLicenseIsRequired = new("187", "The Id of the license is required.");
    public static readonly Error LicenseIdIsEmpty = new("188", "The license id is invalid.");
    public static readonly Error LicenseNameIsEmpty = new("189", "The license name is invalid.");
    public static readonly Error LicenseStartDateGreaterThanEndDate = new("190", "The license start date is greater than the end date.");
    public static readonly Error LicenseMetadataIsNull = new("191", "The license metadata is null.");    
    public static readonly Error LicenseNameIsInvalid = new("192", "The license name is invalid.");
    public static readonly Error IdBuyerIsRequired = new("193", "The Id Buyer is required.");
    public static readonly Error IdPaymentIsRequired = new("194", "The Id Payment is required.");

    public static readonly Error TaxLicenseCannotBeLessThanZero = new("195", "The Tax of the license cannot be less than zero.");

    public static readonly Error DuplicatePricingStrategyFound = new("196", "A duplicate pricing strategy was found.");

    public static readonly Error TotalOfLicenseIsRequired = new("197", "The Total of the license is required.");
    public static readonly Error TaxOfLicenseIsRequired = new("198", "The Tax of the license is required.");
    public static readonly Error SubTotalOfLicenseIsRequired = new("199", "The SubTotal of the license is required.");
    public static readonly Error CurrenciesMustMatch = new("216", "The currencies must match.");

    public static readonly Error ModulesLicenseIsRequired = new("218", "The modules of the license are required.");
    public static readonly Error AttributesLicenseIsRequired = new("219", "The attributes of the license are required.");
    public static readonly Error OrderIsNotSucceeded = new("224", "Cannot get license tenant from an order that has not succeeded.");

    public static readonly Error DescriptionOfLicenseIsRequired = new("225", "The description of the license is required.");
    public static readonly Error ShortDescriptionOfLicenseIsRequired = new("215", "The short description of the license is required.");
    public static readonly Error TermsOfServiceOfLicenseIsRequired = new("217", "The terms of service of the license is required.");
    public static readonly Error NameModuleIsRequired = new("220", "The name of the module is required.");

    public static readonly Error FileAttachmentIdIsInvalid = new("221", "The file attachment id is invalid.");
    public static readonly Error FileAttachmentNameIsInvalid = new("222", "The file attachment name is invalid.");
    public static readonly Error FileAttachmentTargetIsInvalid = new("223", "The file attachment target is invalid.");
}

