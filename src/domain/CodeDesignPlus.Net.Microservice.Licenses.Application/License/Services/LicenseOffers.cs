using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.License.Services;

/// <summary>
/// Comprueba que una licencia privada mejore la de cada copropiedad a la que se ofrece (pendings/464).
/// </summary>
/// <remarks>
/// Una licencia a medida nunca baja la licencia del cliente (decisión del usuario, 2026-11-02): solo suma módulos o
/// atributos, aunque cueste menos. El cambio ya lo exige al cotizar; comprobarlo al ofrecerla le dice al Super Admin que
/// la licencia no le aparecería a esa copropiedad, en vez de dejar una oferta que nadie puede tomar (regla 42). Es la misma
/// cotización del cambio, así que lo que se acepta aquí es lo que el cambio acepta. Se salta la copropiedad que ya tiene
/// esta licencia: para ella ya no es un cambio sino su renovación. El error nombra las copropiedades a las que no mejora.
/// </remarks>
public static class LicenseOffers
{
    /// <summary>
    /// Falla si alguna copropiedad a la que se ofrece la licencia no tiene suscripción o no ganaría nada con ella.
    /// </summary>
    /// <param name="license">La licencia ya creada o editada.</param>
    /// <param name="orders">Las órdenes, para leer la suscripción de cada copropiedad.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Una tarea que termina cuando se comprobaron todas.</returns>
    public static async Task EnsureEachIsAnUpgradeAsync(LicenseAggregate license, IOrderRepository orders, CancellationToken cancellationToken)
    {
        // Una inactiva no se le ofrece a nadie: no hay nada que comprobar hasta que se active.
        if (!license.IsActive)
        {
            return;
        }

        var now = SystemClock.Instance.GetCurrentInstant();
        var notAnUpgrade = new List<string>();

        foreach (var offer in license.OfferedTo ?? [])
        {
            var subscription = SubscriptionTimeline.From(await orders.FindPaidByTenantAsync(offer.TenantId, cancellationToken));

            ApplicationGuard.IsNull(subscription!, Errors.OfferedTenantHasNoSubscription);

            var current = subscription!.CurrentAt(now).License;

            if (current.Id == license.Id)
            {
                continue;
            }

            var quote = SubscriptionChangeQuote.For(subscription, license, Money.Zero(current.Total.Currency), now);

            if (quote is null)
            {
                notAnUpgrade.Add(offer.TenantName);
            }
        }

        // Se nombran todas las que fallan, no solo la primera: el Super Admin corrige la oferta de una vez.
        ApplicationGuard.IsTrue(notAnUpgrade.Count > 0, Errors.LicenseIsNotAnUpgradeForAnOfferedTenant.With(string.Join(", ", notAnUpgrade)));
    }

    /// <summary>
    /// Falla si alguna de estas ofertas ya no se puede retirar: la copropiedad tomó la licencia o tiene un pago en curso
    /// para tomarla (decisión del usuario, 2026-11-04).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tomarla es tener una orden pagada y vigente con esta licencia, sea la de hoy o una ya pagada que empieza después. Si la
    /// copropiedad se cambió luego a otra, esa orden quedó inactiva y retirar la oferta ya no le quita nada.
    /// </para>
    /// <para>
    /// Con un cambio a esta licencia esperando su pago, retirar la oferta dejaría un cobro en curso por algo que ya no se
    /// ofrece; cancelar esa orden desde aquí correría contra el webhook de la pasarela, que puede aprobarla a la vez. Por eso
    /// se bloquea hasta que el pago se resuelva: aprobado, la copropiedad ya la tomó; rechazado o vencido, se puede retirar.
    /// </para>
    /// <para>
    /// El error nombra la primera copropiedad que no se puede, como lo pidió el usuario («No puedes retirar la oferta a
    /// {copropiedad}: ya tomó esta licencia.»).
    /// </para>
    /// </remarks>
    /// <param name="licenseId">La licencia.</param>
    /// <param name="offers">Las ofertas que se van a retirar.</param>
    /// <param name="orders">Las órdenes, para leer qué tomó y qué está pagando cada copropiedad.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Una tarea que termina cuando se revisaron todas.</returns>
    public static async Task EnsureCanWithdrawAsync(Guid licenseId, IEnumerable<LicenseOffer> offers, IOrderRepository orders, CancellationToken cancellationToken)
    {
        foreach (var offer in offers)
        {
            var paid = await orders.FindPaidByTenantAsync(offer.TenantId, cancellationToken);

            ApplicationGuard.IsTrue(paid.Any(x => x.License.Id == licenseId), Errors.OfferAlreadyTaken.With(offer.TenantName));

            var awaiting = await orders.FindAwaitingSubscriptionPaymentAsync(offer.TenantId, cancellationToken);

            ApplicationGuard.IsTrue(awaiting.Any(x => x.Type == OrderType.Change && x.License.Id == licenseId), Errors.OfferHasAPaymentInProgress.With(offer.TenantName));
        }
    }
}
