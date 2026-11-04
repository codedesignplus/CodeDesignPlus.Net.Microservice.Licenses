using Microsoft.Extensions.Options;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Options;

/// <summary>
/// La política de vencimiento de las suscripciones (pendings/735, 736, 737).
/// </summary>
/// <remarks>
/// Es de la plataforma, no de cada copropiedad: la decide ExcellenceForge para todas sus licencias. Por eso vive en el
/// <c>appsettings</c> y no en el código. Los días de gracia no van aquí: son los de <c>Security:GraceDays</c>, los mismos
/// que aplica el middleware de licencias del SDK (pendings/737).
/// </remarks>
public class SubscriptionOptions
{
    /// <summary>
    /// The configuration section.
    /// </summary>
    public static readonly string Section = "Subscription";

    /// <summary>
    /// Los días antes del vencimiento en que se avisa (pendings/736: 30, 15, 7 y 1). El mayor marca desde cuándo la
    /// suscripción se muestra por vencer.
    /// </summary>
    public List<int> ReminderDays { get; set; } = [];

    /// <summary>
    /// Cuántos días después de su día puede salir todavía un aviso que no salió porque el job no corrió.
    /// </summary>
    public int NoticeCatchUpDays { get; set; }

    /// <summary>
    /// Desde cuántos días antes del vencimiento la suscripción se muestra por vencer: el mayor de los avisos.
    /// </summary>
    public int ExpiringWindowDays => ReminderDays.Count == 0 ? 0 : ReminderDays.Max();
}

/// <summary>
/// Comprueba al arrancar que la política de vencimiento está definida.
/// </summary>
public class SubscriptionOptionsValidator : IValidateOptions<SubscriptionOptions>
{
    /// <summary>
    /// Valida la política.
    /// </summary>
    /// <param name="name">The options name.</param>
    /// <param name="options">The options.</param>
    /// <returns>The validation result.</returns>
    public ValidateOptionsResult Validate(string? name, SubscriptionOptions options)
    {
        var failures = new List<string>();

        if (options.ReminderDays.Count == 0 || options.ReminderDays.Any(x => x <= 0))
        {
            failures.Add($"'{nameof(SubscriptionOptions.ReminderDays)}' must list at least one positive number of days.");
        }

        if (options.NoticeCatchUpDays < 0)
        {
            failures.Add($"'{nameof(SubscriptionOptions.NoticeCatchUpDays)}' cannot be negative.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"The section '{SubscriptionOptions.Section}' is not valid: {string.Join(" ", failures)}");
    }
}
