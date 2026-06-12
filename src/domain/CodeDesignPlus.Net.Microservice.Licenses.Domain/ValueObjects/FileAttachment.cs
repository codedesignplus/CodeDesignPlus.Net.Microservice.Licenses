namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class FileAttachment
{
    public Guid Id { get; private set; }
    public string Name { get; private set; } = null!;
    public string Target { get; private set; } = null!;

    public FileAttachment(Guid id, string name, string target)
    {
        DomainGuard.GuidIsEmpty(id, Errors.FileAttachmentIdIsInvalid);
        DomainGuard.IsNullOrEmpty(name, Errors.FileAttachmentNameIsInvalid);
        DomainGuard.IsNullOrEmpty(target, Errors.FileAttachmentTargetIsInvalid);

        Id = id;
        Name = name;
        Target = target;
    }
}
