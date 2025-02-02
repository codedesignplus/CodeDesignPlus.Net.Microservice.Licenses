namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.DeleteLicense;

[DtoGenerator]
public record DeleteLicenseCommand(Guid Id) : IRequest;

public class Validator : AbstractValidator<DeleteLicenseCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
    }
}
