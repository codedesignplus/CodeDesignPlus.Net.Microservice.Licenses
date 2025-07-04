namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

public class Errors : IErrorCodes
{
    public const string UnknownError = "100 : UnknownError";
    public const string IdLicenseIsRequired = "101 : The Id of the license is required.";
    public const string NameLicenseIsRequired = "102 : The Name of the license is required.";
    public const string DescriptionLicenseIsRequired = "103 : The Description of the license is required.";
    public const string CurrencyLicenseIsRequired = "104 : The Currency of the license is required.";
    public const string PriceLicenseCannotBeLessThanZero = "105 : The Price of the license cannot be less than zero.";
    public const string CreatedByLicenseIsRequired = "106 : The CreatedBy of the license is required.";
    public const string NameCurrencyIsRequired = "107 : The Name of the currency is required.";
    public const string CodeCurrencyIsRequired = "108 : The Code of the currency is required.";
    public const string SymbolCurrencyIsRequired = "109 : The Symbol of the currency is required.";
    public const string IdModuleIsRequired = "110 : The Id of the module is required.";
    public const string ModuleAlreadyExists = "111 : The module already exists in the license.";
    public const string ModuleNotFound = "112 : The module was not found.";
    public const string PriceLicenseIsRequired = "113 : The Price of the license is required.";
    public const string DescriptionModuleIsRequired = "114 : The Description of the module is required.";
    public const string IconLicenseIsRequired = "115 : The Icon of the license is required.";
    public const string ShortDescriptionLicenseIsRequired = "116 : The description of license is required.";
    public const string DiscountLicenseCannotBeLessThanZero = "117 : The Discount of the license cannot be less than zero.";
    public const string ColorLicenseIsRequired = "118 : The Color of the license is required.";
    public const string NameIsRequired = "119 : The Name is required.";
    public const string NameIsTooLong = "120 : The Name is too long, maximum length is 124 characters.";
    public const string PhoneIsRequired = "121 : The Phone is required.";
    public const string PhoneContainsInvalidCharacters = "122 : The Phone contains invalid characters, it should be a valid phone number format.";
    public const string EmailIsRequired = "123 : The Email is required.";
    public const string EmailContainsInvalidCharacters = "124 : The Email contains invalid characters, it should be a valid email format.";
    public const string TypeDocumentIsRequired = "125 : The TypeDocument is required.";
    public const string TypeDocumentIsTooLong = "126 : The TypeDocument is too long, maximum length is 3 characters.";
    public const string DocumentIsRequired = "127 : The Document is required.";
    public const string DocumentIsTooLong = "128 : The Document is too long, maximum length is 20 characters.";
    public const string AddressIsRequired = "129 : The Address is required.";
    public const string AddressIsTooLong = "130 : The Address is too long, maximum length is 256 characters.";
    public const string AddressContainsInvalidCharacters = "131 : The Address contains invalid characters, it should be a valid address format.";
    public const string PostalCodeIsRequired = "132 : The PostalCode is required.";
    public const string PostalCodeIsTooLong = "133 : The PostalCode is too long, maximum length is 16 characters.";
    public const string PostalCodeContainsInvalidCharacters = "134 : The PostalCode contains invalid characters, it should be a valid postal code format.";
    public const string CityNameIsEmpty = "135 : The city name is invalid.";
    public const string CityIdIsEmpty = "136 : The city id is invalid.";
    public const string WebContainsInvalidCharacters = "137 : The Web contains invalid characters, it should be a valid URL format.";
    public const string LocationIsRequired = "138 : The Location is required.";
    public const string CurrencyIdIsEmpty = "139 : The Currency Id is empty.";
    public const string CountryNameIsEmpty = "140 : The country name is invalid.";
    public const string CountryIdIsEmpty = "141 : The country id is invalid.";
    public const string CountryCodeIsInvalid = "142 : The country code is invalid.";
    public const string CountryTimeZoneIsEmpty = "143 : The country timeZone is invalid.";
    public const string LocalityNameIsEmpty = "144 : The locality name is invalid.";
    public const string LocalityIdIsEmpty = "145 : The locality id is invalid.";
    public const string NeighborhoodNameIsEmpty = "146 : The neighborhood name is invalid.";
    public const string NeighborhoodIdIsEmpty = "147 : The neighborhood id is invalid.";
    public const string StateNameIsEmpty = "148 : The state name is invalid.";
    public const string StateIdIsEmpty = "149 : The state id is invalid.";
    public const string StateCodeIsEmpty = "150 : The state code is invalid.";
    public const string CountryIsNull = "151 : The country is null.";
    public const string StateIsNull = "152 : The state is null.";
    public const string CityIsNull = "153 : The city is null.";
    public const string LocalityIsNull = "154 : The locality is null.";
    public const string NeighborhoodIsNull = "155 : The neighborhood is null.";
    public const string PseCodeCannotBeNullOrEmpty = "156 : Pse Code cannot be null or empty";
    public const string PseCodeCannotBeGreaterThan34Characters = "157 : Pse Code cannot be greater than 34 characters";
    public const string PseCodeMustBeValidFormat = "158 : Pse Code must be valid format";
    public const string TypePersonCannotBeNullOrEmpty = "159 : Type Person cannot be null or empty";
    public const string TypePersonCannotBeGreaterThan2Characters = "160 : Type Person cannot be greater than 2 characters";
    public const string PseCannotBeNull = "163 : Pse cannot be null";
    public const string CreditCardNumberCannotBeNullOrEmpty  = "164 : Credit Card Number cannot be null or empty";
    public const string CreditCardNumberCannotBeGreaterThan20Characters  = "165 : Credit Card Number cannot be greater than 20 characters"; 
    public const string CreditCardNumberCannotBeLessThan13Characters = "166 : Credit Card Number cannot be less than 13 characters"; 
    public const string CreditCardSecurityCodeCannotBeNullOrEmpty = "167 : Credit Card Security Code cannot be null or empty"; 
    public const string CreditCardSecurityCodeCannotBeGreaterThan4Characters = "168 : Credit Card Security Code cannot be greater than 4 characters"; 
    public const string CreditCardSecurityCodeCannotBeLessThan3Characters = "169 : Credit Card Security Code cannot be less than 3 characters"; 
    public const string CreditCardExpirationDateCannotBeNullOrEmpty = "170 : Credit Card Expiration Date cannot be null or empty"; 
    public const string CreditCardExpirationDateMustBeValidFormat = "171 : Credit Card Expiration Date must be valid format";
    public const string CreditCardExpirationDateCannotBeGreaterThan7Characters = "172 : Credit Card Expiration Date cannot be greater than 7 characters";
    public const string CreditCardCannotBeNull = "173 : Credit Card cannot be null";
    public const string PaymentMethodIsRequired = "174 : The Payment Method is required."; 
    public const string BuyerIsRequired = "175 : The Buyer is required."; 
    public const string TenantDetailIsRequired = "176 : The Tenant Detail is required.";
    public const string CardHolderNameCannotBeNullOrEmpty = "177 : Card Holder Name cannot be null or empty";
    public const string IdOrderIsRequired = "178 : The Id Order is required.";
    public const string LicenseIdIsRequired = "179 : The License Id is required.";
    public const string TotalIsInvalid = "180 : The Total is invalid.";
    public const string TaxIsInvalid = "181 : The Tax is invalid."; 
    public const string SubTotalIsInvalid = "182 : The SubTotal is invalid.";
    public const string IdTenantIsRequired = "183 : The Id Tenant is required.";
    public const string CountryAlpha2IsEmpty = "184 : The Country Alpha2 is empty.";

    public const string CodeOfThePaymentMethodIsRequired = "185 : The code of the payment method is required.";

    public const string PaymentResponseIsRequired = "186 : The Payment Response is required.";

    public const string PseResponseUrlCannotBeNullOrEmpty = "187 : The Pse Response URL cannot be null or empty."; 
    public const string PseResponseUrlCannotBeGreaterThan200Characters = "188 : The Pse Response URL cannot be greater than 200 characters.";

    public const string PseResponseUrlMustBeValidFormat = "189 : The Pse Response URL must be a valid URL.";

    public const string NotifyUrlIsRequired = "190 : The Notify URL is required.";
    public const string NotifyUrlMustBeValidFormat = "191 : The Notify URL must be a valid URL.";

    public const string CodeTypeDocumentCannotBeNullOrEmpty = "192 : The Code Type Document cannot be null or empty.";
    public const string NameTypeDocumentIsInvalid = "193 : The Name Type Document is invalid.";
    public const string CodeTypeDocumentIsInvalid = "194 : The Code Type Document is invalid.";
    public const string NumberDocumentIsRequired = "195 : The Number Document is required.";

    public const string NameOfLicenseIsRequired = "196 : The Name of the license is required."; 
    public const string TotalOfLicenseShouldBeGreaterThanZero  = "197 : The Total of the license should be greater than zero.";
    public const string TaxOfLicenseShouldBeGreaterThanZero = "198 : The Tax of the license should be greater than zero."; 
    public const string SubTotalOfLicenseShouldBeGreaterThanZero = "199 : The SubTotal of the license should be greater than zero.";

    public const string TotalIsNotEqualToTaxAndSubTotal = "200 : The Total is not equal to the sum of Tax and SubTotal.";

    public const string IdOfLicenseIsRequired = "201 : The Id of the license is required.";
}

