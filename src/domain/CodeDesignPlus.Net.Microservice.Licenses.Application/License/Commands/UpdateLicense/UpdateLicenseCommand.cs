using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;

[DtoGenerator]
public record UpdateLicenseCommand(Guid Id, string Name, string ShortDescription, string Description, List<ModuleDto> Modules, List<Price> Prices, Icon Icon, string TermsOfService, Dictionary<string, string> Attributes, bool IsActive, bool IsPopular) : IRequest;

public class Validator : AbstractValidator<UpdateLicenseCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
        RuleFor(x => x.Name).NotEmpty().NotNull().MaximumLength(128);
        RuleFor(x => x.Description).NotEmpty().NotNull().MaximumLength(512);
        RuleFor(x => x.Prices).NotNull().NotEmpty();
        RuleFor(x => x.Icon).NotNull();
        RuleFor(x => x.TermsOfService).NotEmpty().NotNull().MaximumLength(20480);
        RuleFor(x => x.ShortDescription).NotEmpty().NotNull().MaximumLength(100);
    }
}
