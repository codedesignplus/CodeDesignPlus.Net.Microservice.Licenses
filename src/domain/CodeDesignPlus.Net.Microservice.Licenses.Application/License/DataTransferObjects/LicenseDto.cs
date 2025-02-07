using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;

public class LicenseDto: IDtoBase
{
    public required Guid Id { get; set; }
    public string Name { get;  set; } = null!;
    public string Description { get;  set; } = null!;
    public List<ModuleDto> Modules { get;  set; } = [];
    public BillingTypeEnum BillingType { get;  set; }
    public Currency Currency { get;  set; } = null!;
    public long Price { get;  set; }
    public Dictionary<string, string> Attributes { get;  set; } = [];
}