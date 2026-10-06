using CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Persistence;
using CodeDesignPlus.Net.Mongo.Extensions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Card = CodeDesignPlus.Net.ValueObjects.Payment.CreditCard;
using Method = CodeDesignPlus.Net.ValueObjects.Payment.PaymentMethod;

namespace CodeDesignPlus.Net.Microservice.Licenses.Infrastructure.Test.Persistence;

/// <summary>
/// El pedido de una licencia guarda el medio de pago: el código de seguridad de la tarjeta no puede llegar a Mongo
/// (pendings/322).
/// </summary>
public class CreditCardClassMapTest
{
    private const string SecurityCodeProbe = "cvv-probe-322";

    public CreditCardClassMapTest()
    {
        MongoSerializerRegistration.RegisterSerializers();
        MongoClassMaps.Register();
    }

    [Fact]
    public void ToBsonDocument_CardPaymentMethod_DoesNotStoreSecurityCode()
    {
        // Arrange
        var method = BuildCardMethod();

        // Act
        var document = method.ToBsonDocument();

        // Assert
        Assert.DoesNotContain(SecurityCodeProbe, document.ToJson(), StringComparison.Ordinal);
    }

    [Fact]
    public void Deserialize_StoredCardWithoutSecurityCode_ReadsTheCard()
    {
        // Arrange
        var document = BuildCardMethod().ToBsonDocument();
        document["CreditCard"].AsBsonDocument.Remove("SecurityCode");

        // Act
        var method = BsonSerializer.Deserialize<Method>(document);

        // Assert
        Assert.Equal("4242", method.CreditCard!.Last4Digits);
    }

    private static Method BuildCardMethod() => Method.Create("VISA", null, Card.Create("tok-probe", "4242", "2030/12", "COPROPIEDAD MALPELO", SecurityCodeProbe));
}
