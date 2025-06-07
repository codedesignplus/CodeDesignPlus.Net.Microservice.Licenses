namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.PayLicense;

[DtoGenerator]
public record PayLicenseCommand(Guid Id) : IRequest;

public class Validator : AbstractValidator<PayLicenseCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
    }
}
