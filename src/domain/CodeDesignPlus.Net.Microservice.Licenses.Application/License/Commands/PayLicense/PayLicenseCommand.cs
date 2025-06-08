using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.PayLicense;

[DtoGenerator]
public record PayLicenseCommand(Guid Id, PaymentMethod PaymentMethod, Buyer Buyer, Organization Organization) : IRequest;

public class Validator : AbstractValidator<PayLicenseCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
        RuleFor(x => x.PaymentMethod).NotNull().WithMessage("The MethodPay is required.");
        RuleFor(x => x.Buyer).NotNull().WithMessage("The Buyer is required.");
        RuleFor(x => x.Organization).NotNull().WithMessage("The Organization is required.");
    }
}
