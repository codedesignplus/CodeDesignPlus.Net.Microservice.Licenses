using CodeDesignPlus.Net.Exceptions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application;

public class Errors: IErrorCodes
{    
    public static readonly Error UnknownError = new("200", "UnknownError");

    public static readonly Error InvalidRequest = new("201", "The request is invalid."); 
    public static readonly Error LicenseAlreadyExists = new("202", "The license already exists."); 
    public static readonly Error LicenseNotFound = new("203", "The license was not found.");
    public static readonly Error LicensePopularityAlreadyExists = new("204", "The license popularity already exists.");

    public static readonly Error PriceLicenseChangedOrIsNotValid = new("205", "The price of the license has changed or is not valid.");
    public static readonly Error PaymentFailed = new("206", "The payment failed because {0}");

    public static readonly Error OrderNotFound = new("207", "The order was not found.");

    public static readonly Error TenantAlreadyExists = new("208", "The tenant already exists.");

    public static readonly Error OrderAlreadyExists = new("209", "The order already exists.");

    public static readonly Error PriceInvalid = new("210", "The price for the selected billing type and model is not available.");

    public static readonly Error InvalidCurrency = new("211", "The order currency does not match the tenant's country currency.");

    public static readonly Error PriceNotFoundBecauseBillingTypeIsNotAvailableInTheLicense = new("212", "The price for the selected billing type is not available in the license.");

    public static readonly Error TenantIdIsRequired = new("213", "The tenant ID is required.");

    public static readonly Error LicenseIdIsRequired = new("214", "The license ID is required.");
}
