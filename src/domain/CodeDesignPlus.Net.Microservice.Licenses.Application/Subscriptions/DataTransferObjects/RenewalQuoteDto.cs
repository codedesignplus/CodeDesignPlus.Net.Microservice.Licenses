using CodeDesignPlus.Net.Microservice.Licenses.Application.Package.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.DataTransferObjects;

/// <summary>
/// Cuánto cuesta renovar la licencia con sus paquetes (pendings/735, 739). Importes en unidades mínimas.
/// </summary>
public class RenewalQuoteDto
{
    /// <summary>
    /// La moneda.
    /// </summary>
    public string Currency { get; set; } = string.Empty;

    /// <summary>
    /// La periodicidad de la licencia.
    /// </summary>
    public BillingType BillingType { get; set; }

    /// <summary>
    /// El precio vigente de la licencia.
    /// </summary>
    public PackagePriceDto License { get; set; } = new();

    /// <summary>
    /// Los paquetes que se renuevan.
    /// </summary>
    public List<PackageQuoteDto> Packages { get; set; } = [];

    /// <summary>
    /// El precio completo: licencia más paquetes.
    /// </summary>
    public long Total { get; set; }

    /// <summary>
    /// El saldo a favor que se aplica.
    /// </summary>
    public long CreditBalanceApplied { get; set; }

    /// <summary>
    /// Lo que cobra la pasarela.
    /// </summary>
    public long AmountDue { get; set; }

    /// <summary>
    /// El saldo a favor que queda.
    /// </summary>
    public long RemainingCreditBalance { get; set; }
}
