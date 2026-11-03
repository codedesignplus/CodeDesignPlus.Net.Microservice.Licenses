using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.User;
using LicenseSnapshot = CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects.License;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions;

/// <summary>
/// Las copias que guarda una orden de renovación o de cambio de licencia (pendings/735, 738).
/// </summary>
public static class SubscriptionSnapshots
{
    /// <summary>
    /// La licencia que se renueva: la misma copia que tiene la copropiedad (sus módulos y atributos están fijados por
    /// contrato, plan 451) con el precio vigente del catálogo.
    /// </summary>
    /// <param name="current">La copia de la licencia en curso.</param>
    /// <param name="price">El precio vigente del catálogo para su periodicidad.</param>
    /// <param name="invoiceConcept">El concepto de la factura vigente en el catálogo.</param>
    /// <returns>La copia para la orden.</returns>
    public static LicenseSnapshot Renewal(LicenseSnapshot current, Price price, string? invoiceConcept) => LicenseSnapshot.Create(
        current.Id,
        current.Name,
        price.Total,
        price.Tax,
        price.SubTotal,
        price.BillingType,
        price.BillingModel,
        current.Description,
        current.ShortDescription,
        current.Icon,
        current.TermsOfService,
        current.IsPopular,
        current.ShowInLandingPage,
        new Dictionary<string, string>(current.Attributes),
        [.. current.Modules]).WithBillingTerms(price, invoiceConcept);

    /// <summary>
    /// La licencia a la que se cambia: la copia del catálogo con sus módulos y atributos y su precio completo.
    /// </summary>
    /// <param name="target">La licencia del catálogo.</param>
    /// <param name="price">Su precio para la periodicidad de la copropiedad.</param>
    /// <returns>La copia para la orden.</returns>
    public static LicenseSnapshot Of(LicenseAggregate target, Price price) => LicenseSnapshot.Create(
        target.Id,
        target.Name,
        price.Total,
        price.Tax,
        price.SubTotal,
        price.BillingType,
        price.BillingModel,
        target.Description,
        target.ShortDescription,
        target.Icon,
        target.TermsOfService,
        target.IsPopular,
        target.ShowInLandingPage,
        new Dictionary<string, string>(target.Attributes),
        [.. target.Modules.Select(m => new LicenseModule(m.Id, m.Name, m.Description))]).WithBillingTerms(price, target.InvoiceConcept);

    /// <summary>
    /// Quien paga: el usuario que lo pide, con los datos de facturación de la orden anterior.
    /// </summary>
    /// <param name="previous">El comprador de la orden en curso.</param>
    /// <param name="userId">El usuario que pide la renovación o el cambio.</param>
    /// <returns>El comprador de la orden nueva.</returns>
    public static Buyer Payer(Buyer previous, Guid userId)
    {
        if (previous.TypeDocument is null || string.IsNullOrWhiteSpace(previous.Document))
        {
            return Buyer.CreateMinimal(userId, previous.Name, previous.Phone, previous.Email);
        }

        return previous.ShippingAddress is null
            ? Buyer.CreateWithoutShipping(userId, previous.Name, previous.Phone, previous.Email, previous.TypeDocument, previous.Document)
            : Buyer.Create(userId, previous.Name, previous.Phone, previous.Email, previous.TypeDocument, previous.Document, previous.ShippingAddress);
    }
}
