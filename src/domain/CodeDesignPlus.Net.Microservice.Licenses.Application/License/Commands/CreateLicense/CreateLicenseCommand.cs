using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.CreateLicense;

[DtoGenerator]
public record CreateLicenseCommand(Guid Id, string Name, string ShortDescription, string Description, List<ModuleDto> Modules, List<PriceDto> Prices, Icon Icon, string TermsOfService, Dictionary<string, string> Attributes, bool IsActive, bool IsPopular, bool ShowInLandingPage) : IRequest;

public class Validator : AbstractValidator<CreateLicenseCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
        RuleFor(x => x.Name).NotEmpty().NotNull().MaximumLength(128);
        RuleFor(x => x.Description).NotEmpty().NotNull().MaximumLength(512);
        RuleFor(x => x.Prices).NotEmpty().NotNull();
        RuleFor(x => x.Icon).NotNull();
        RuleFor(x => x.TermsOfService).NotEmpty().NotNull().MaximumLength(20480);
        RuleFor(x => x.ShortDescription).NotEmpty().NotNull().MaximumLength(100);
    }
}
