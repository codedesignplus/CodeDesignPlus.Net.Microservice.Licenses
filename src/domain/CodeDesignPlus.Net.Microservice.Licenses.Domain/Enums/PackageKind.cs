namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

/// <summary>
/// Cómo se arma un paquete complementario (pendings/739): con atributos y precio fijos, o con componentes que el cliente
/// elige y de los que sale el precio.
/// </summary>
public enum PackageKind
{
    /// <summary>
    /// Atributos con su valor y un precio anual fijo.
    /// </summary>
    Fixed = 0,

    /// <summary>
    /// Componentes con su rango y su precio; el precio sale de lo que elige el cliente.
    /// </summary>
    Configurable = 1
}
