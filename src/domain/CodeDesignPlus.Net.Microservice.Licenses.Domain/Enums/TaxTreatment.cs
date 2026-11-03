namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

/// <summary>
/// Cómo trata el impuesto el precio de una licencia (pendings/662): gravado con su tarifa, excluido o exento.
/// </summary>
/// <remarks>
/// Para la factura electrónica son tres cosas distintas: un excluido no causa el impuesto, un exento lo causa a tarifa
/// cero y un gravado lo causa a su tarifa. El valor por defecto es el gravado: los precios guardados antes de existir el
/// campo llevaban todos su tarifa.
/// </remarks>
public enum TaxTreatment
{
    /// <summary>
    /// Gravado: el precio suma el impuesto a su tarifa, que es mayor que cero.
    /// </summary>
    Taxed = 0,

    /// <summary>
    /// Excluido: el precio no causa el impuesto; lleva su base legal.
    /// </summary>
    Excluded = 1,

    /// <summary>
    /// Exento: el precio causa el impuesto a tarifa cero; lleva su base legal.
    /// </summary>
    Exempt = 2
}
