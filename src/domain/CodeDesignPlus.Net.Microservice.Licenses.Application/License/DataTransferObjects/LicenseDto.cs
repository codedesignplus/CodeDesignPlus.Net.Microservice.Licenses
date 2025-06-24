using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;

public class LicenseDto : IDtoBase
{
    public required Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public string ShortDescription { get; set; } = null!;
    public string Description { get; set; } = null!;
    public List<ModuleDto> Modules { get; set; } = [];
    public List<Price> Prices { get; set; } = [];
    public Icon Icon { get; set; } = null!;
    public string TermsOfService { get; set; } = null!;
    public Dictionary<string, string> Attributes { get; set; } = [];
    public bool IsActive { get; set; }
    public bool IsPopular { get; set; }
    public bool ShowInLandingPage { get; set; }
}