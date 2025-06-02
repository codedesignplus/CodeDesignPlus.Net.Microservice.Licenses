namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.AddModule;

[DtoGenerator]
public record AddModuleCommand(Guid Id, Guid IdModule, string Name, string Description) : IRequest;

public class Validator : AbstractValidator<AddModuleCommand>
{
    public Validator()
    {
        RuleFor(x => x.IdModule).NotEmpty().NotNull();
        RuleFor(x => x.Name).NotEmpty().NotNull().MaximumLength(128);
        RuleFor(x => x.Description).NotEmpty().NotNull();
    }
}
