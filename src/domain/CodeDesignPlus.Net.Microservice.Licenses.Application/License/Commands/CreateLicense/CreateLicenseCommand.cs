using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.CreateLicense;

[DtoGenerator]
public record CreateLicenseCommand(Guid Id, string Name, string Description, List<ModuleDto> Modules, BillingTypeEnum BillingType, Currency Currency, long Price, Dictionary<string, string> Attributes) : IRequest;

public class Validator : AbstractValidator<CreateLicenseCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
        RuleFor(x => x.Name).NotEmpty().NotNull().MaximumLength(128);
        RuleFor(x => x.Description).NotEmpty().NotNull().MaximumLength(512);
    }
}
