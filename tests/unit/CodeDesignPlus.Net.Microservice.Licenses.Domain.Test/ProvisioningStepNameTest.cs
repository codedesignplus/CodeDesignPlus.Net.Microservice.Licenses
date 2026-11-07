using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test;

/// <summary>
/// The step names of an order's history are the keys the Super Admin's order detail translates to Spanish (pendings/674).
/// </summary>
/// <remarks>
/// They are stored in every order, so a renamed key leaves the stored history with no name on the screen and nothing fails.
/// The frontend lists the same keys in <c>services/ms-licenses/order-steps.ts</c>.
/// </remarks>
public class ProvisioningStepNameTest
{
    /// <summary>
    /// Each step and the key it is stored with.
    /// </summary>
    public static TheoryData<string, string> Steps => new()
    {
        { ProvisioningStep.Payment, "Payment" },
        { ProvisioningStep.TenantProvisioning, "TenantProvisioning" },
        { ProvisioningStep.UserProvisioning, "UserProvisioning" },
        { ProvisioningStep.Provisioning, "Provisioning" },
        { ProvisioningStep.LicenseApplication, "LicenseApplication" },
    };

    [Theory]
    [MemberData(nameof(Steps))]
    public void StepName_AlreadyStored_KeepsItsKey(string step, string stored)
    {
        // Assert
        Assert.Equal(stored, step);
    }

    [Fact]
    public void RequiredForPurchase_IsTheTenantAndItsUser()
    {
        // Assert
        Assert.Equal(["TenantProvisioning", "UserProvisioning"], ProvisioningStep.RequiredForPurchase);
    }
}
