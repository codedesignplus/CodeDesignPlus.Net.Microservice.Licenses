namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.CompleteProvisioningStep;

public record CompleteProvisioningStepCommand(Guid OrderId, string StepName) : IRequest;

public class Validator : AbstractValidator<CompleteProvisioningStepCommand>
{
    public Validator()
    {
        RuleFor(x => x.OrderId).NotEmpty().NotNull();
        RuleFor(x => x.StepName).NotEmpty().NotNull();
    }
}
