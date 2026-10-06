using MongoDB.Bson.Serialization;
using Card = CodeDesignPlus.Net.ValueObjects.Payment.CreditCard;

namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Persistence;

/// <summary>
/// Mapas de Mongo que deciden qué no se guarda nunca, sea cual sea el agregado que lo lleve.
/// </summary>
/// <remarks>
/// El código de seguridad de la tarjeta viaja dentro del objeto de valor <see cref="Card"/>, porque es lo que ms-payments
/// necesita para autorizar el cobro. Pero ese mismo objeto es el que el pedido de la licencia guarda, y PCI DSS (requisito
/// 3.2) prohíbe conservar el CVV después de la autorización, incluso cifrado: se guardaba en claro (pendings/322).
/// <para>
/// Se corta en el mapa y no en cada agregado porque así ningún documento de este micro puede llevarlo, tampoco uno
/// que se escriba mañana con el mismo objeto de valor. En memoria sigue disponible hasta que se pide el cobro a
/// ms-payments, que es el único sitio donde hace falta.
/// </para>
/// </remarks>
public static class MongoClassMaps
{
    private static readonly Lock Gate = new();

    /// <summary>
    /// Registra los mapas. Es idempotente, y falla si alguien ya mapeó la tarjeta con el código de seguridad dentro.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the card was already mapped including its security code.</exception>
    public static void Register()
    {
        lock (Gate)
        {
            if (!BsonClassMap.IsClassMapRegistered(typeof(Card)))
            {
                BsonClassMap.RegisterClassMap<Card>(map =>
                {
                    map.AutoMap();
                    map.UnmapProperty(card => card.SecurityCode);
                });
            }

            // Si algo serializó la tarjeta antes de este registro, Mongo ya creó su mapa automático, que sí guarda el
            // CVV. Callarlo sería volver a guardarlo sin que nadie se entere.
            var registered = BsonClassMap.LookupClassMap(typeof(Card));

            if (registered.GetMemberMap(nameof(Card.SecurityCode)) is not null)
            {
                throw new InvalidOperationException("The credit card was mapped for MongoDB with its security code before the licenses class maps were registered.");
            }
        }
    }
}
