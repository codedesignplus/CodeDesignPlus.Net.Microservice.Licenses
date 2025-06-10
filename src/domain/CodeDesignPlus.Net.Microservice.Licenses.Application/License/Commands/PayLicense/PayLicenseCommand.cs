using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.PayLicense;

[DtoGenerator]
public record PayLicenseCommand(Guid Id, Order Order, PaymentMethod PaymentMethod, Organization Organization) : IRequest;

public class Validator : AbstractValidator<PayLicenseCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
        RuleFor(x => x.PaymentMethod).NotNull().WithMessage("The MethodPay is required.");
        RuleFor(x => x.Order).NotNull().WithMessage("The Order is required.");
        RuleFor(x => x.Organization).NotNull().WithMessage("The Organization is required.");
    }
}
