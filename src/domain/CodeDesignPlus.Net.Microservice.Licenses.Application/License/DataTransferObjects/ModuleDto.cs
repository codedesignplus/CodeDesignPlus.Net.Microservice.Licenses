using System;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;

public class ModuleDto : IDtoBase
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
}
