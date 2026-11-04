using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;
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
/// esta licencia: para ella ya no es un cambio sino su renovación. Los errores nombran las copropiedades sin suscripción pagada y
/// aquellas a las que no mejora.
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
        var withoutSubscription = new List<string>();
        var notAnUpgrade = new List<string>();

        foreach (var offer in license.OfferedTo ?? [])
        {
            var subscription = SubscriptionTimeline.From(await orders.FindPaidByTenantAsync(offer.TenantId, cancellationToken));

            if (subscription is null)
            {
                withoutSubscription.Add(offer.TenantName);

                continue;
            }

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
        ApplicationGuard.IsTrue(withoutSubscription.Count > 0, Errors.OfferedTenantHasNoSubscription.With(string.Join(", ", withoutSubscription)));
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
            var status = await StatusOfAsync(licenseId, offer.TenantId, orders, cancellationToken);

            ApplicationGuard.IsTrue(status.Taken, Errors.OfferAlreadyTaken.With(offer.TenantName));

            ApplicationGuard.IsTrue(status.PaymentInProgress, Errors.OfferHasAPaymentInProgress.With(offer.TenantName));
        }
    }

    /// <summary>
    /// Fills, in each offer of a license read by the Super Admin, whether the tenant already took it and whether it is paying
    /// for it, so the screen does not offer to withdraw what would then be rejected (rule 42, pendings/464).
    /// </summary>
    /// <remarks>
    /// It is computed on every read and never cached: it depends on the orders, not on the license.
    /// </remarks>
    /// <param name="license">The license read by the Super Admin.</param>
    /// <param name="orders">The orders, to read what each tenant took and what it is paying.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when every offer was filled.</returns>
    public static async Task FillOfferStatusAsync(LicenseDto license, IOrderRepository orders, CancellationToken cancellationToken)
    {
        foreach (var offer in license.OfferedTo ?? [])
        {
            var status = await StatusOfAsync(license.Id, offer.TenantId, orders, cancellationToken);

            offer.Taken = status.Taken;
            offer.PaymentInProgress = status.PaymentInProgress;
        }
    }

    /// <summary>
    /// Whether a tenant took this license or is paying for a change to it. It is the only place that decides it: the
    /// withdrawal errors (273 and 274) and the flags the Super Admin reads come from here.
    /// </summary>
    /// <param name="licenseId">The license.</param>
    /// <param name="tenantId">The tenant it is offered to.</param>
    /// <param name="orders">The orders, to read what the tenant took and what it is paying.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The status of the offer.</returns>
    public static async Task<OfferStatus> StatusOfAsync(Guid licenseId, Guid tenantId, IOrderRepository orders, CancellationToken cancellationToken)
    {
        // Took it: a paid and active order with this license, the current one or an already paid one that starts later.
        var paid = await orders.FindPaidByTenantAsync(tenantId, cancellationToken);

        // Paying it: a change to this license still waiting for its payment.
        var awaiting = await orders.FindAwaitingSubscriptionPaymentAsync(tenantId, cancellationToken);

        return new OfferStatus(
            paid.Any(x => x.License.Id == licenseId),
            awaiting.Any(x => x.Type == OrderType.Change && x.License.Id == licenseId));
    }
}

/// <summary>
/// The status of an offer of a tailored license to a tenant (pendings/464).
/// </summary>
/// <param name="Taken">The tenant took the license: it has a paid and active order with it.</param>
/// <param name="PaymentInProgress">The tenant has a change to this license waiting for its payment.</param>
public sealed record OfferStatus(bool Taken, bool PaymentInProgress);
