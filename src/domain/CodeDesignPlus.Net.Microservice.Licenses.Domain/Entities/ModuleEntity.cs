namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;

public class ModuleEntity : IEntityBase
{
    public Guid Id { get; set; }
    public required string Name { get; set; }
    public required string Description { get; set; }
}
