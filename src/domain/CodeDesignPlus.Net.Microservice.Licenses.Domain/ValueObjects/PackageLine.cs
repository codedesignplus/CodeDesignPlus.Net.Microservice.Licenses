using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Un paquete comprado en una orden: la copia de lo que se eligió, con sus atributos y su precio, que no cambia aunque el
/// catálogo cambie (pendings/739), igual que la copia de la licencia.
/// </summary>
/// <param name="LineId">El identificador de la línea: la ampliación y la renovación lo citan en <see cref="ReplacesLineId"/>.</param>
/// <param name="PackageId">El paquete del catálogo.</param>
/// <param name="Name">Su nombre al comprarlo.</param>
/// <param name="Kind">Fijo o configurable.</param>
/// <param name="Selections">Lo elegido, por clave de componente; vacío en un fijo.</param>
/// <param name="Attributes">Los atributos que aporta, con su combinación.</param>
/// <param name="SubTotal">La base sin impuestos.</param>
/// <param name="Tax">El impuesto.</param>
/// <param name="Total">El total.</param>
/// <param name="TaxTreatment">Cómo trató el impuesto.</param>
/// <param name="TaxBasisPoints">La tarifa en puntos básicos.</param>
/// <param name="TaxLegalBasis">La base legal de un excluido o exento.</param>
/// <param name="InvoiceConcept">El concepto de la factura.</param>
/// <param name="ReplacesLineId">La línea que reemplaza: la que se amplía, se renueva o se recotiza en un cambio.</param>
public sealed record PackageLine(Guid LineId, Guid PackageId, string Name, PackageKind Kind, Dictionary<string, string> Selections, List<PackageAttribute> Attributes, Money SubTotal, Money Tax, Money Total, TaxTreatment TaxTreatment, int TaxBasisPoints, string? TaxLegalBasis, string? InvoiceConcept, Guid? ReplacesLineId)
{
    /// <summary>
    /// El inicio del periodo de la línea; se fija al aprobarse el pago.
    /// </summary>
    public Instant? PeriodStart { get; private set; }

    /// <summary>
    /// El fin del periodo de la línea; se fija al aprobarse el pago.
    /// </summary>
    public Instant? PeriodEnd { get; private set; }

    /// <summary>
    /// Crea la línea, aún sin periodo.
    /// </summary>
    /// <returns>La línea.</returns>
    public static PackageLine Create(Guid lineId, Guid packageId, string name, PackageKind kind, Dictionary<string, string> selections, List<PackageAttribute> attributes, Money subTotal, Money tax, Money total, TaxTreatment taxTreatment, int taxBasisPoints, string? taxLegalBasis, string? invoiceConcept, Guid? replacesLineId)
    {
        DomainGuard.GuidIsEmpty(lineId, Errors.PackageLineIsInvalid);
        DomainGuard.GuidIsEmpty(packageId, Errors.PackageLineIsInvalid);
        DomainGuard.IsTrue(string.IsNullOrWhiteSpace(name), Errors.PackageLineIsInvalid);
        DomainGuard.IsTrue(attributes is null || attributes.Count == 0, Errors.PackageLineIsInvalid);
        DomainGuard.IsTrue(subTotal is null || tax is null || total is null, Errors.PackageLineIsInvalid);
        DomainGuard.IsTrue(subTotal!.Currency != total!.Currency || tax!.Currency != total.Currency || subTotal + tax != total || total.Amount <= 0, Errors.PackageLineIsInvalid);

        return new PackageLine(lineId, packageId, name, kind, selections ?? [], attributes!, subTotal, tax!, total, taxTreatment, taxBasisPoints, taxLegalBasis, invoiceConcept, replacesLineId);
    }

    /// <summary>
    /// La misma línea con su periodo.
    /// </summary>
    /// <param name="start">El inicio.</param>
    /// <param name="end">El fin.</param>
    /// <returns>La línea con periodo.</returns>
    public PackageLine WithPeriod(Instant start, Instant end) => this with { PeriodStart = start, PeriodEnd = end < start ? start : end };

    /// <summary>
    /// Si cuenta en un instante por su periodo (sin mirar si la licencia está pagada ni si otra la reemplazó).
    /// </summary>
    /// <param name="now">El instante.</param>
    /// <returns><c>true</c> si el instante cae en su periodo.</returns>
    public bool CoversAt(Instant now) => PeriodStart is not null && PeriodEnd is not null && PeriodStart.Value <= now && now < PeriodEnd.Value;
}
