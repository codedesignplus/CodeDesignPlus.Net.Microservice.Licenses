using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;

[DtoGenerator]
public record UpdateLicenseCommand(Guid Id, string Name, string Description, List<ModuleDto> Modules, BillingTypeEnum BillingType, Currency Currency, long Price, Dictionary<string, string> Attributes) : IRequest;

public class Validator : AbstractValidator<UpdateLicenseCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
    }
}
