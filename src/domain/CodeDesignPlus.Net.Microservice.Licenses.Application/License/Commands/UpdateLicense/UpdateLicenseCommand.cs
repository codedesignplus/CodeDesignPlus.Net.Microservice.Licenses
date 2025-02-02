namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;

[DtoGenerator]
public record UpdateLicenseCommand(Guid Id) : IRequest;

public class Validator : AbstractValidator<UpdateLicenseCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
    }
}
