namespace CodeDesignPlus.Net.Microservice.Licenses.Application;

public class Errors: IErrorCodes
{    
    public const string UnknownError = "200 : UnknownError";

    public const string InvalidRequest = "201 : The request is invalid."; 
    public const string LicenseAlreadyExists = "202 : The license already exists."; 
    public const string LicenseNotFound = "203 : The license was not found.";
    public const string LicensePopularityAlreadyExists = "204 : The license popularity already exists.";

    public const string PriceLicenseChangedOrIsNotValid = "205 : The price of the license has changed or is not valid.";
    public const string PaymentFailed = "206 : The payment failed because {0}";

    public const string OrderNotFound = "207 : The order was not found.";

    public const string TenantAlreadyExists = "208 : The tenant already exists.";

    public const string OrderAlreadyExists = "209 : The order already exists.";
}
