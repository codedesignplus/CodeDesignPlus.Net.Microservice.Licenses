namespace CodeDesignPlus.Net.Microservice.Licenses.Application;

public class Errors: IErrorCodes
{    
    public const string UnknownError = "200 : UnknownError";

    public const string InvalidRequest = "201 : The request is invalid."; 
    public const string LicenseAlreadyExists = "202 : The license already exists."; 
    public const string LicenseNotFound = "203 : The license was not found.";
    public const string LicensePopularityAlreadyExists = "204 : The license popularity already exists.";

    public const string PriceLicenseChangedOrIsNotValid = "205 : The price of the license has changed or is not valid.";
}
