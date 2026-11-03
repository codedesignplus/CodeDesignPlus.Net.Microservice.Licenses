using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Package.Commands.CreatePackage;

/// <summary>
/// Crea un paquete complementario del catálogo (pendings/739). Precios en unidad mayor y la tarifa en porcentaje (reglas 04
/// y 05).
/// </summary>
/// <param name="Id">El identificador, generado por el cliente una vez (regla 66).</param>
/// <param name="Name">El nombre.</param>
/// <param name="ShortDescription">La descripción corta.</param>
/// <param name="Description">La descripción.</param>
/// <param name="Icon">El ícono.</param>
/// <param name="IsActive">Si está activo.</param>
/// <param name="IsPopular">Si se destaca.</param>
/// <param name="ShowInLandingPage">Si es público.</param>
/// <param name="OfferedTo">Las copropiedades a las que se ofrece si es privado.</param>
/// <param name="Kind">Fijo o configurable.</param>
/// <param name="Currency">La moneda.</param>
/// <param name="BasePrice">El precio del fijo o la base del configurable, en unidad mayor.</param>
/// <param name="MinimumPrice">El piso del configurable, en unidad mayor.</param>
/// <param name="TaxTreatment">El tratamiento del impuesto.</param>
/// <param name="TaxPercentage">La tarifa en porcentaje.</param>
/// <param name="TaxLegalBasis">La base legal de un excluido o exento.</param>
/// <param name="InvoiceConcept">El concepto de la factura.</param>
/// <param name="Attributes">Los atributos del fijo.</param>
/// <param name="Components">Los componentes del configurable.</param>
[DtoGenerator]
public record CreatePackageCommand(Guid Id, string Name, string ShortDescription, string Description, Icon Icon, bool IsActive, bool IsPopular, bool ShowInLandingPage, List<OfferedTenant>? OfferedTo, PackageKind Kind, string Currency, decimal BasePrice, decimal MinimumPrice, TaxTreatment TaxTreatment, decimal TaxPercentage, string? TaxLegalBasis, string InvoiceConcept, List<PackageAttributeInput>? Attributes, List<PackageComponentInput>? Components) : IRequest;

/// <summary>
/// Valida el comando.
/// </summary>
public class Validator : AbstractValidator<CreatePackageCommand>
{
    /// <summary>
    /// Crea el validador.
    /// </summary>
    public Validator()
    {
        RuleFor(x => x.Id).NotEmpty();
        RuleFor(x => x.Name).NotEmpty().MaximumLength(FieldLength.Name);
        RuleFor(x => x.ShortDescription).NotEmpty().MaximumLength(FieldLength.Title);
        RuleFor(x => x.Description).NotEmpty().MaximumLength(FieldLength.Description);
        RuleFor(x => x.Icon).NotNull();
        RuleFor(x => x.Kind).IsInEnum();
        RuleFor(x => x.Currency).NotEmpty().Length(3);
        RuleFor(x => x.TaxTreatment).IsInEnum();
        RuleFor(x => x.TaxLegalBasis).MaximumLength(FieldLength.Title);
        RuleFor(x => x.InvoiceConcept).NotEmpty().MaximumLength(FieldLength.Title);
        RuleForEach(x => x.Attributes).ChildRules(attribute =>
        {
            attribute.RuleFor(x => x.Key).NotEmpty().MaximumLength(FieldLength.Name);
            attribute.RuleFor(x => x.Value).NotEmpty().MaximumLength(FieldLength.Name);
            attribute.RuleFor(x => x.Combine).IsInEnum();
        });
        RuleForEach(x => x.Components).ChildRules(component =>
        {
            component.RuleFor(x => x.Key).NotEmpty().MaximumLength(FieldLength.Name);
            component.RuleFor(x => x.Kind).IsInEnum();
            component.RuleFor(x => x.Combine).IsInEnum();
            component.RuleFor(x => x.PerUnitOf).MaximumLength(FieldLength.Name);
            component.RuleFor(x => x.EnableKey).MaximumLength(FieldLength.Name);
        });
        RuleForEach(x => x.OfferedTo).ChildRules(tenant =>
        {
            tenant.RuleFor(x => x.TenantId).NotEmpty();
            tenant.RuleFor(x => x.TenantName).NotEmpty().MaximumLength(FieldLength.Name);
        });
    }
}
