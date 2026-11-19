namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

/// <summary>
/// Qué elige el cliente en un componente de un paquete configurable (pendings/739).
/// </summary>
public enum PackageComponentKind
{
    /// <summary>
    /// Una cantidad entera entre un mínimo y un máximo, con su paso; se cobra por unidad.
    /// </summary>
    Quantity = 0,

    /// <summary>
    /// Uno de unos tramos con su precio (p. ej. conectados a la vez: 100, 200, 350).
    /// </summary>
    Tiered = 1,

    /// <summary>
    /// Sí o no; se cobra si está encendido.
    /// </summary>
    Toggle = 2,

    /// <summary>
    /// Una cantidad entera cuyo precio por unidad lo fija el tramo de volumen en que cae lo elegido (p. ej. de 1 a 50, de 51
    /// a 200): toda la cantidad se cobra al precio de su tramo. Puede entregar un múltiplo de lo elegido (pendings/744).
    /// </summary>
    Volume = 3
}
