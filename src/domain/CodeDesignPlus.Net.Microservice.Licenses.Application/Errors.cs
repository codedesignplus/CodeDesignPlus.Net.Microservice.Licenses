using CodeDesignPlus.Net.Exceptions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application;

public class Errors: IErrorCodes
{    
    public static readonly Error UnknownError = new("200");

    public static readonly Error InvalidRequest = new("201"); 
    public static readonly Error LicenseAlreadyExists = new("202"); 
    public static readonly Error LicenseNotFound = new("203");
    public static readonly Error LicensePopularityAlreadyExists = new("204");

    public static readonly Error PriceLicenseChangedOrIsNotValid = new("205");
    public static readonly Error PaymentFailed = new("206");

    public static readonly Error OrderNotFound = new("207");

    public static readonly Error TenantAlreadyExists = new("208");

    public static readonly Error OrderAlreadyExists = new("209");

    public static readonly Error PriceInvalid = new("210");

    public static readonly Error InvalidCurrency = new("211");

    public static readonly Error PriceNotFoundBecauseBillingTypeIsNotAvailableInTheLicense = new("212");

    public static readonly Error TenantIdIsRequired = new("213");

    public static readonly Error LicenseIdIsRequired = new("214");
}
