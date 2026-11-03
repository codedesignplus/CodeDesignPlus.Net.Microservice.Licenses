using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Test.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.Test;

/// <summary>
/// El tratamiento tributario del precio de una licencia (pendings/662): gravado con su tarifa, o excluido o exento con su
/// base legal, sin un 19 % por defecto, y copiado en la orden para facturarla.
/// </summary>
public class PriceTaxTreatmentTest
{
    private const string LegalBasis = "Art. 476 num. 21 ET";

    private static Price Monthly(TaxTreatment treatment, int taxBasisPoints, string? legalBasis)
        => Price.Create(BillingType.Monthly, Money.FromLong(12_000_000, "COP"), BillingModel.FlatRate, 0, treatment, taxBasisPoints, legalBasis);

    /// <summary>
    /// Un precio excluido no suma impuesto: el total es la base.
    /// </summary>
    [Fact]
    public void Create_AnExcludedPrice_HasNoTax()
    {
        // Act
        var price = Monthly(TaxTreatment.Excluded, 0, LegalBasis);

        // Assert
        Assert.Equal((0L, 12_000_000L, LegalBasis), (price.Tax.Amount, price.Total.Amount, price.TaxLegalBasis));
    }

    /// <summary>
    /// Cada combinación que no cuadra con el tratamiento se rechaza con su motivo.
    /// </summary>
    [Theory]
    [InlineData(TaxTreatment.Taxed, 0, null, "165")]
    [InlineData(TaxTreatment.Taxed, 1900, LegalBasis, "166")]
    [InlineData(TaxTreatment.Excluded, 1900, LegalBasis, "169")]
    [InlineData(TaxTreatment.Exempt, 0, " ", "174")]
    [InlineData((TaxTreatment)9, 0, LegalBasis, "162")]
    public void Create_ARateOrLegalBasisThatDoesNotMatchTheTreatment_IsRejected(TaxTreatment treatment, int taxBasisPoints, string? legalBasis, string code)
    {
        // Act
        var exception = Assert.Throws<CodeDesignPlusException>(() => Monthly(treatment, taxBasisPoints, legalBasis));

        // Assert
        Assert.Equal(code, exception.Code);
    }

    /// <summary>
    /// Un precio guardado antes de existir el tratamiento se sigue leyendo (regla 31) y es gravado, que es lo que eran
    /// todos.
    /// </summary>
    [Fact]
    public void Deserialize_APriceStoredBeforeTheTreatment_ReadsAsTaxed()
    {
        // Arrange
        CodeDesignPlus.Net.Mongo.Extensions.MongoSerializerRegistration.RegisterSerializers();

        var document = Monthly(TaxTreatment.Excluded, 0, LegalBasis).ToBsonDocument();

        document.Remove("TaxTreatment");
        document.Remove("TaxLegalBasis");

        // Act
        var stored = BsonSerializer.Deserialize<Price>(document);

        // Assert
        Assert.Equal((TaxTreatment.Taxed, (string?)null), (stored.TaxTreatment, stored.TaxLegalBasis));
    }

    /// <summary>
    /// La orden copia la tarifa, el tratamiento, la base legal y el concepto de la factura, y los lleva en el evento del
    /// pago para que quien facture no lea el catálogo.
    /// </summary>
    [Fact]
    public void ToBilling_AnOrderOfAnExcludedPrice_CarriesTheTreatmentAndTheInvoiceConcept()
    {
        // Arrange
        var price = Monthly(TaxTreatment.Excluded, 0, LegalBasis);
        var license = SubscriptionOrders.Monthly(subTotal: 12_000_000, tax: 0).WithBillingTerms(price, "Suscripción al servicio en la nube Kappali (SaaS)");
        var order = SubscriptionOrders.PaidRenewal(SubscriptionOrders.Bogota(2027, 3, 1), license);

        // Act
        var billing = order.ToBilling(SubscriptionOrders.Bogota(2027, 3, 1, 10));

        // Assert
        Assert.Equal((TaxTreatment.Excluded, 0, LegalBasis, "Suscripción al servicio en la nube Kappali (SaaS)"), (billing.TaxTreatment, billing.TaxBasisPoints, billing.TaxLegalBasis, billing.InvoiceConcept));
    }

    /// <summary>
    /// Una orden guardada antes de existir los términos aceptados y el tratamiento de su licencia se sigue leyendo
    /// (regla 31).
    /// </summary>
    [Fact]
    public void Deserialize_AnOrderStoredBeforeTheTermsAndTheTreatment_StillReads()
    {
        // Arrange
        CodeDesignPlus.Net.Mongo.Extensions.MongoSerializerRegistration.RegisterSerializers();

        var license = SubscriptionOrders.Monthly().WithBillingTerms(Monthly(TaxTreatment.Taxed, 1900, null), "Concepto");
        var order = SubscriptionOrders.Paid(SubscriptionOrders.Bogota(2027, 3, 1), license);
        order.RecordAcceptedTerms("2026-11-02");

        var document = order.ToBsonDocument();
        var snapshot = document["License"].AsBsonDocument;

        document.Remove("AcceptedTermsVersion");

        foreach (var field in new[] { "TaxBasisPoints", "TaxTreatment", "TaxLegalBasis", "InvoiceConcept" })
        {
            snapshot.Remove(field);
        }

        // Act
        var stored = BsonSerializer.Deserialize<OrderAggregate>(document);

        // Assert
        Assert.Equal(((string?)null, 0, TaxTreatment.Taxed, (string?)null), (stored.AcceptedTermsVersion, stored.License.TaxBasisPoints, stored.License.TaxTreatment, stored.License.InvoiceConcept));
    }
}
