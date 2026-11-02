using Microsoft.Extensions.Options;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Options;

/// <summary>
/// What the online payment of an order needs from the environment.
/// </summary>
public class OnlinePaymentOptions
{
    /// <summary>
    /// The configuration section.
    /// </summary>
    public static readonly string Section = "OnlinePayment";

    /// <summary>
    /// The absolute URL of the page the payment provider returns the buyer to when they finish paying.
    /// </summary>
    /// <remarks>
    /// It is a page of the frontend, so it changes with the environment and lives in the <c>appsettings</c>, not in
    /// the code. That page asks ms-payments for the outcome; it never reads what the provider appends to the URL.
    /// </remarks>
    public string ReturnUrl { get; set; } = string.Empty;

    /// <summary>
    /// The absolute URL of the page the payment provider returns the administrator to after paying the renewal or the
    /// change of the license of an existing tenant (pendings/735, 738).
    /// </summary>
    /// <remarks>
    /// It is not the purchase page: there is no tenant to provision. It is the payment return page of Configuración ›
    /// Organización (<c>/apps/settings/organization/payment-return</c>), which asks for the outcome with the payment id
    /// and goes back to the organization.
    /// </remarks>
    public string SubscriptionReturnUrl { get; set; } = string.Empty;
}

/// <summary>
/// Checks on start that the environment defined an absolute return URL.
/// </summary>
public class OnlinePaymentOptionsValidator : IValidateOptions<OnlinePaymentOptions>
{
    /// <summary>
    /// Validates that the return URL is an absolute http or https URL.
    /// </summary>
    /// <param name="name">The options name.</param>
    /// <param name="options">The options.</param>
    /// <returns>The validation result.</returns>
    public ValidateOptionsResult Validate(string? name, OnlinePaymentOptions options)
    {
        var invalid = new[] { (nameof(OnlinePaymentOptions.ReturnUrl), options.ReturnUrl), (nameof(OnlinePaymentOptions.SubscriptionReturnUrl), options.SubscriptionReturnUrl) }
            .Where(x => !IsAbsoluteHttp(x.Item2))
            .Select(x => x.Item1)
            .ToList();

        if (invalid.Count == 0)
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail($"The section '{OnlinePaymentOptions.Section}' must define {string.Join(" and ", invalid.Select(x => $"'{x}'"))} as an absolute http or https URL.");
    }

    private static bool IsAbsoluteHttp(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp);
}
