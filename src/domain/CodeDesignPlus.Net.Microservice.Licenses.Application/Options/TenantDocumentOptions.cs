using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Microsoft.Extensions.Options;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Options;

/// <summary>
/// Qué tipos de documento de una copropiedad llevan dígito de verificación y con qué pesos se calcula.
/// </summary>
/// <remarks>
/// Es configuración del despliegue y no una lista en el código: este micro es transversal y no lleva un país dentro. El
/// tipo es un código del catálogo de tipos de documento, y cuál lleva dígito y con qué pesos lo decide la autoridad
/// tributaria del país del despliegue. Sin la sección, ningún tipo exige dígito. ms-tenants lleva la misma sección con los
/// mismos valores: la copropiedad que esta compra cobra la tiene que poder crear él (pendings/744).
/// </remarks>
public class TenantDocumentOptions
{
    /// <summary>
    /// La sección de configuración.
    /// </summary>
    public static readonly string Section = "TenantDocument";

    /// <summary>
    /// Los códigos de tipo de documento cuyo número se escribe con guion y dígito de verificación.
    /// </summary>
    public List<string> CheckDigitDocumentTypes { get; set; } = [];

    /// <summary>
    /// Los pesos del módulo 11 ponderado, del dígito de las unidades hacia la izquierda.
    /// </summary>
    public List<int> CheckDigitWeights { get; set; } = [];

    /// <summary>
    /// Las reglas del dígito de verificación como las entiende el dominio.
    /// </summary>
    /// <returns>Los tipos y los pesos configurados.</returns>
    public DocumentCheckDigitRules ToRules()
    {
        return new DocumentCheckDigitRules(this.CheckDigitDocumentTypes, this.CheckDigitWeights);
    }
}

/// <summary>
/// Comprueba al arrancar que los tipos con dígito de verificación tienen con qué calcularlo.
/// </summary>
/// <remarks>
/// Se valida al arrancar y no al usarse: con tipos y sin pesos, todo documento de esos tipos se rechazaría sin que nadie
/// supiera por qué, y con un peso que no es positivo el dígito calculado sería otro.
/// </remarks>
public class TenantDocumentOptionsValidator : IValidateOptions<TenantDocumentOptions>
{
    /// <summary>
    /// Valida la sección.
    /// </summary>
    /// <param name="name">El nombre de la instancia de opciones.</param>
    /// <param name="options">La sección leída.</param>
    /// <returns>El resultado de la validación.</returns>
    public ValidateOptionsResult Validate(string? name, TenantDocumentOptions options)
    {
        if (options.CheckDigitDocumentTypes.Exists(string.IsNullOrWhiteSpace))
        {
            return ValidateOptionsResult.Fail($"La seccion '{TenantDocumentOptions.Section}' tiene un codigo vacio en {nameof(TenantDocumentOptions.CheckDigitDocumentTypes)}.");
        }

        if (options.CheckDigitDocumentTypes.Count > 0 && options.CheckDigitWeights.Count == 0)
        {
            return ValidateOptionsResult.Fail($"La seccion '{TenantDocumentOptions.Section}' define tipos con digito de verificacion pero no {nameof(TenantDocumentOptions.CheckDigitWeights)}: sin pesos no hay con que calcularlo.");
        }

        if (options.CheckDigitWeights.Exists(weight => weight <= 0))
        {
            return ValidateOptionsResult.Fail($"La seccion '{TenantDocumentOptions.Section}' tiene un peso que no es positivo en {nameof(TenantDocumentOptions.CheckDigitWeights)}.");
        }

        return ValidateOptionsResult.Success;
    }
}
