namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Un paquete pagado tal como lo aplica ms-tenants a la copropiedad (pendings/739): sus atributos con su combinación y su
/// periodo.
/// </summary>
/// <remarks>
/// La combinación viaja como texto (<c>Sum</c>, <c>Max</c>, <c>Or</c>) y no como número: un número cambia de significado
/// en cuanto alguien reordena el enum en un solo micro (regla 26, §4).
/// </remarks>
/// <param name="LineId">La línea de la orden.</param>
/// <param name="PackageId">El paquete del catálogo.</param>
/// <param name="Name">Su nombre.</param>
/// <param name="StartDate">El inicio de su periodo.</param>
/// <param name="EndDate">El fin de su periodo.</param>
/// <param name="ReplacesLineId">La línea que reemplaza desde <paramref name="StartDate"/>.</param>
/// <param name="Attributes">Los atributos que aporta.</param>
public sealed record PackageTenant(Guid LineId, Guid PackageId, string Name, Instant StartDate, Instant EndDate, Guid? ReplacesLineId, List<PackageTenantAttribute> Attributes)
{
    /// <summary>
    /// La copia de una línea pagada.
    /// </summary>
    /// <param name="line">La línea con su periodo.</param>
    /// <returns>El paquete para ms-tenants.</returns>
    public static PackageTenant Of(PackageLine line)
    {
        DomainGuard.IsTrue(line.PeriodStart is null || line.PeriodEnd is null, Errors.OrderHasNoPeriod);

        return new PackageTenant(line.LineId, line.PackageId, line.Name, line.PeriodStart!.Value, line.PeriodEnd!.Value, line.ReplacesLineId, [.. line.Attributes.Select(x => new PackageTenantAttribute(x.Key, x.Value, x.Combination.ToString()))]);
    }
}

/// <summary>
/// Un atributo de un paquete para ms-tenants.
/// </summary>
/// <param name="Key">La clave.</param>
/// <param name="Value">El valor.</param>
/// <param name="Combination">La combinación como texto: <c>Sum</c>, <c>Max</c> u <c>Or</c>.</param>
public sealed record PackageTenantAttribute(string Key, string Value, string Combination);
