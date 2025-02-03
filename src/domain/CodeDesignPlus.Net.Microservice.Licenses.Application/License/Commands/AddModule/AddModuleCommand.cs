namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.AddModule;

[DtoGenerator]
public record AddModuleCommand(Guid Id, Guid IdModule, string Name) : IRequest;

public class Validator : AbstractValidator<AddModuleCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
    }
}
