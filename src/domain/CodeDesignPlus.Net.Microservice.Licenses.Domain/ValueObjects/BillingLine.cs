using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Una línea de la factura de una orden: la licencia o un paquete, cada uno con su concepto y su impuesto (pendings/739,
/// 661).
/// </summary>
/// <param name="Concept">El concepto de la factura.</param>
/// <param name="PackageLineId">La línea del paquete; nula para la licencia.</param>
/// <param name="SubTotal">La base sin impuestos.</param>
/// <param name="Tax">El impuesto.</param>
/// <param name="Total">El total.</param>
/// <param name="TaxTreatment">Cómo trata el impuesto.</param>
/// <param name="TaxBasisPoints">La tarifa en puntos básicos.</param>
/// <param name="TaxLegalBasis">La base legal de un excluido o exento.</param>
/// <param name="PeriodStart">El inicio del periodo facturado de la línea.</param>
/// <param name="PeriodEnd">El fin del periodo facturado de la línea.</param>
public sealed record BillingLine(string Concept, Guid? PackageLineId, Money SubTotal, Money Tax, Money Total, TaxTreatment TaxTreatment, int TaxBasisPoints, string? TaxLegalBasis, Instant PeriodStart, Instant PeriodEnd);
