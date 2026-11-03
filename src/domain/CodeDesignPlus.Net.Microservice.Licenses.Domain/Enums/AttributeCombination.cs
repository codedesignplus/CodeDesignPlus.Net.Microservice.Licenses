namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

/// <summary>
/// Cómo se combina el valor de un atributo de un paquete con el de la licencia y el de los demás paquetes (pendings/739).
/// </summary>
/// <remarks>
/// La declara el catálogo por atributo: el micro no sabe qué significa cada clave, solo cómo sumarla.
/// </remarks>
public enum AttributeCombination
{
    /// <summary>
    /// Se suman los enteros (p. ej. reuniones al año).
    /// </summary>
    Sum = 0,

    /// <summary>
    /// Se toma el mayor de los enteros (p. ej. conectados a la vez).
    /// </summary>
    Max = 1,

    /// <summary>
    /// Se hace «o» de los sí/no (p. ej. grabación).
    /// </summary>
    Or = 2
}
