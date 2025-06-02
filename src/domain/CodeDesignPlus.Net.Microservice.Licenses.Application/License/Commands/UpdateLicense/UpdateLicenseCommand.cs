using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;

[DtoGenerator]
public record UpdateLicenseCommand(Guid Id, string Name, string Description, List<ModuleDto> Modules, List<Price> Prices, Guid IdLogo, string TermsOfService, Dictionary<string, string> Attributes, bool IsActive) : IRequest;

public class Validator : AbstractValidator<UpdateLicenseCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
        RuleFor(x => x.Name).NotEmpty().NotNull().MaximumLength(128);
        RuleFor(x => x.Description).NotEmpty().NotNull().MaximumLength(512);
        RuleFor(x => x.Prices).NotNull().NotEmpty();
        RuleFor(x => x.IdLogo).NotEmpty().NotNull();
        RuleFor(x => x.TermsOfService).NotEmpty().NotNull().MaximumLength(4092);
    }
}
