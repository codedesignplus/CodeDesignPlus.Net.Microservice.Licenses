namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.CreateLicense;

[DtoGenerator]
public record CreateLicenseCommand(Guid Id) : IRequest;

public class Validator : AbstractValidator<CreateLicenseCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
    }
}
