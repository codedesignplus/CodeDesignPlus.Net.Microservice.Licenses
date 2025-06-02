namespace CodeDesignPlus.Net.Microservice.Licenses.Domain;

public class Errors: IErrorCodes
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
}
