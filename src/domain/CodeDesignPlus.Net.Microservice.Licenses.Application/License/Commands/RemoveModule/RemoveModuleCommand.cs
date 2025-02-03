namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.RemoveModule;

[DtoGenerator]
public record RemoveModuleCommand(Guid Id, Guid IdModule) : IRequest;

public class Validator : AbstractValidator<RemoveModuleCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
    }
}
