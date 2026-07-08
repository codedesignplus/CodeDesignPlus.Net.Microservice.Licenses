namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.FailProvisioningStep;

public record FailProvisioningStepCommand(Guid OrderId, string StepName, string Error) : IRequest;

public class Validator : AbstractValidator<FailProvisioningStepCommand>
{
    public Validator()
    {
        RuleFor(x => x.OrderId).NotEmpty().NotNull();
        RuleFor(x => x.StepName).NotEmpty().NotNull();
        RuleFor(x => x.Error).NotEmpty().NotNull();
    }
}
