using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class TypeDocument
{
    public string Code { get; private set; } 

    public string Name { get; private set; }

    [JsonConstructor]
    private TypeDocument(string code, string name)
    {
        DomainGuard.IsNullOrEmpty(code, Errors.CodeTypeDocumentCannotBeNullOrEmpty);
        DomainGuard.IsNullOrEmpty(name, Errors.NameTypeDocumentIsInvalid);
        DomainGuard.IsGreaterThan(code.Length, 3, Errors.CodeTypeDocumentIsInvalid);

        this.Code = code;
        this.Name = name;
    }

    public static TypeDocument Create(string code, string name)
    {
        return new TypeDocument(code, name);
    }
}
