namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;

/// <summary>
/// Para qué es una orden: la compra que crea la copropiedad, la renovación de su licencia o el cambio a otra mayor
/// (pendings/735, 738).
/// </summary>
/// <remarks>
/// El valor por defecto es la compra: las órdenes guardadas antes de existir el campo lo eran todas.
/// </remarks>
public enum OrderType
{
    /// <summary>
    /// La compra inicial: al pagarse crea la copropiedad.
    /// </summary>
    Purchase = 0,

    /// <summary>
    /// La renovación de la licencia vigente: al pagarse, el periodo siguiente.
    /// </summary>
    Renewal = 1,

    /// <summary>
    /// El cambio a una licencia mayor: al pagarse empieza un periodo nuevo con ella y la anterior se inactiva.
    /// </summary>
    Change = 2,

    /// <summary>
    /// La compra o la ampliación de un paquete complementario para una copropiedad que ya existe (pendings/739): no toca la
    /// licencia ni su periodo.
    /// </summary>
    Package = 3
}
