namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

public enum ProvisioningStatus
{
    None,
    PaymentPending,
    PaymentSucceeded,
    PaymentFailed,
    InProgress,
    Completed,
    PartiallyFailed
}
