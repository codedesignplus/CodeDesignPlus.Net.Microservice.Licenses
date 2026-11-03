using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.CreateLicense;

[DtoGenerator]
public record CreateLicenseCommand(Guid Id, string Name, string ShortDescription, string Description, List<ModuleDto> Modules, List<PriceInput> Prices, Icon Icon, string TermsOfService, Dictionary<string, string> Attributes, bool IsActive, bool IsPopular, bool ShowInLandingPage, string InvoiceConcept, List<OfferedTenant>? OfferedTo) : IRequest;

public class Validator : AbstractValidator<CreateLicenseCommand>
{
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty().NotNull();
        RuleFor(x => x.Name).NotEmpty().NotNull().MaximumLength(FieldLength.Name);
        RuleFor(x => x.Description).NotEmpty().NotNull().MaximumLength(FieldLength.Description);
        RuleFor(x => x.Prices).NotEmpty().NotNull();
        RuleFor(x => x.Icon).NotNull();
        RuleFor(x => x.TermsOfService).NotEmpty().NotNull().MaximumLength(20480);
        RuleFor(x => x.ShortDescription).NotEmpty().NotNull().MaximumLength(100);
        RuleFor(x => x.InvoiceConcept).NotEmpty().MaximumLength(FieldLength.Title);
        RuleForEach(x => x.Prices).ChildRules(price =>
        {
            price.RuleFor(x => x.TaxTreatment).IsInEnum();
            price.RuleFor(x => x.TaxLegalBasis).MaximumLength(FieldLength.Title);
        });
        RuleForEach(x => x.OfferedTo).ChildRules(tenant =>
        {
            tenant.RuleFor(x => x.TenantId).NotEmpty();
            tenant.RuleFor(x => x.TenantName).NotEmpty().MaximumLength(FieldLength.Name);
        });
    }
}
