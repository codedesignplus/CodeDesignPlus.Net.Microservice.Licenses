using CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
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
/// esta licencia: para ella ya no es un cambio sino su renovación.
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

            ApplicationGuard.IsNull(quote!, Errors.LicenseIsNotAnUpgradeForAnOfferedTenant);
        }
    }
}
