
using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Represents an immutable snapshot of an icon associated with a license.
/// </summary>
public sealed partial record Icon
{
    /// <summary>
    /// The name of the icon.
    /// </summary>
    public string Name { get; private set; } = null!;
    /// <summary>
    /// The color of the icon.
    /// </summary>
    public string Color { get; private set; } = null!;

    [JsonConstructor]
    private Icon(string name, string color)
    {
        DomainGuard.IsNullOrEmpty(name, Errors.IconLicenseIsRequired);
        DomainGuard.IsNullOrEmpty(color, Errors.ColorLicenseIsRequired);

        this.Name = name;
        this.Color = color;
    }

    /// <summary>
    /// Creates a new immutable snapshot of an icon associated with a license.
    /// </summary>
    /// <param name="name">The name of the icon.</param>
    /// <param name="color">The color of the icon.</param>
    /// <returns>A new immutable snapshot of an icon.</returns>
    public static Icon Create(string name, string color)
    {
        return new Icon(name, color);
    }
}
