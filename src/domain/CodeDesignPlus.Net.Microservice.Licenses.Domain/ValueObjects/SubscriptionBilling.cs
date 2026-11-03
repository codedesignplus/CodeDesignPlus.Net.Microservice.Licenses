using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Lo que necesita la factura electrónica de una renovación o de un cambio de licencia (pendings/661, 663): viaja en el
/// evento del pago para que quien facture no tenga que leer la orden.
/// </summary>
/// <param name="SubTotal">La base del precio completo de la licencia, sin impuestos.</param>
/// <param name="Tax">Los impuestos del precio completo.</param>
/// <param name="Total">El precio completo con impuestos: lo que dice la factura.</param>
/// <param name="ProrationCredit">Lo no prestado de la licencia anterior que se acreditó (cero en una renovación).</param>
/// <param name="AdvanceApplied">El anticipo aplicado: el crédito y el saldo a favor que pagan parte del total.</param>
/// <param name="AmountPaid">Lo que se cobró por la pasarela: el total menos el anticipo.</param>
/// <param name="PeriodStart">El inicio del periodo facturado.</param>
/// <param name="PeriodEnd">El fin del periodo facturado.</param>
/// <param name="PaidAt">Cuándo se aprobó el pago: la fecha de la factura.</param>
/// <param name="TaxTreatment">Cómo trata el impuesto el precio: gravado, excluido o exento (pendings/662).</param>
/// <param name="TaxBasisPoints">La tarifa del impuesto en puntos básicos; cero si no es gravado.</param>
/// <param name="TaxLegalBasis">La base legal de un precio excluido o exento.</param>
/// <param name="InvoiceConcept">El concepto de la factura.</param>
public sealed record SubscriptionBilling(Money SubTotal, Money Tax, Money Total, Money ProrationCredit, Money AdvanceApplied, Money AmountPaid, Instant PeriodStart, Instant PeriodEnd, Instant PaidAt, TaxTreatment TaxTreatment, int TaxBasisPoints, string? TaxLegalBasis, string? InvoiceConcept);
