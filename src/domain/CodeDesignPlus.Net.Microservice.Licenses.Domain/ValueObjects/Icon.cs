
using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Icon
{
    public string Name { get; private set; } = null!;
    public string Color { get; private set; } = null!;

    [JsonConstructor]
    private Icon(string name, string color)
    {
        DomainGuard.IsNullOrEmpty(name, Errors.IconLicenseIsRequired);
        DomainGuard.IsNullOrEmpty(color, Errors.ColorLicenseIsRequired);

        this.Name = name;
        this.Color = color;
    }

    public static Icon Create(string name, string color)
    {
        return new Icon(name, color);
    }
}
