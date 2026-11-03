namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;

/// <summary>
/// La regla del nombre de una licencia: la misma al crearla o editarla en el catálogo y al aplicarla a una copropiedad
/// (pendings/738).
/// </summary>
/// <remarks>
/// Admite los nombres comerciales: letras de cualquier idioma (con tildes, ñ y diéresis), dígitos, espacios y los signos
/// <c>· - + . , &amp; ( ) / '</c>; al menos una letra o un dígito, y hasta <see cref="FieldLength.Name"/> caracteres
/// (regla 48). Antes el catálogo solo pedía un nombre no vacío y la copia que se aplica a la copropiedad solo aceptaba
/// letras y espacios: «Élite Plus · Conjunto Prueba Licencias» se creó, se cotizó y se cobró, y el cambio no pudo
/// aplicarse. ms-tenants valida su copia con esta misma regla (<c>Tenants.Domain.ServiceDomain.LicenseName</c>); si
/// cambia aquí, cambia allí.
/// </remarks>
public static partial class LicenseName
{
    [GeneratedRegex(@"^(?=.*[\p{L}\p{N}])[\p{L}\p{M}\p{N}\p{Zs}·\-+.,&()/']+$")]
    private static partial Regex Pattern();

    /// <summary>
    /// Si el nombre, sin los espacios de los extremos, cumple la regla.
    /// </summary>
    /// <param name="name">El nombre.</param>
    /// <returns><see langword="true"/> si se puede usar como nombre de una licencia.</returns>
    public static bool IsValid(string? name)
    {
        var trimmed = name?.Trim();

        if (string.IsNullOrEmpty(trimmed) || trimmed.Length > FieldLength.Name)
        {
            return false;
        }

        return Pattern().IsMatch(trimmed);
    }
}
