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

    public static readonly Error LicenseIsNotActive = new("228");

    public static readonly Error SubscriptionNotFound = new("229");
    public static readonly Error LicenseCannotBeRenewed = new("230");
    public static readonly Error LicenseIsNotAnUpgrade = new("231");
    public static readonly Error LicenseIsNotOffered = new("232");
    public static readonly Error SubscriptionOrderBelongsToAnotherTenant = new("233");
    public static readonly Error LicenseIsNotForSale = new("234");
    public static readonly Error LicenseIsNotAnUpgradeForAnOfferedTenant = new("235");
    public static readonly Error OfferedTenantHasNoSubscription = new("236");

    // Paquetes complementarios (pendings/739).
    public static readonly Error PackageNotFound = new("265");
    public static readonly Error PackageIsNotOffered = new("266");
    public static readonly Error PackageIsNotAnUpgrade = new("267");
    public static readonly Error PackageCurrencyDoesNotMatch = new("268");
    public static readonly Error LicenseIsNotPaidUp = new("269");
    public static readonly Error PackageIsRepeated = new("270");
    public static readonly Error PackageIsNotForSale = new("271");
    public static readonly Error PackagesRenewOnlyWithAnnualLicense = new("272");
    public static readonly Error OfferAlreadyTaken = new("273");
    public static readonly Error OfferHasAPaymentInProgress = new("274");
}
