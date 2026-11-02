using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.ChangeSubscription;

/// <summary>
/// Cambia la licencia de la copropiedad autenticada por una mayor: crea la orden del cambio por el precio completo de la
/// licencia nueva, con lo no prestado de la anterior y el saldo a favor como anticipo, y pide el cobro de lo que falte
/// (pendings/738).
/// </summary>
/// <remarks>
/// Solo hacia arriba (decisión del usuario, 2026-11-02): la licencia destino tiene que ofrecerse a la copropiedad, tener
/// precio para su periodicidad y tener todos sus módulos y atributos iguales o mayores. El importe es el de la cotización
/// de <c>GET Subscription/change-options</c>, porque sale del mismo cálculo. Al pagarse, la licencia nueva empieza un
/// periodo completo ese día y las órdenes acreditadas se inactivan.
/// </remarks>
public class ChangeSubscriptionCommandHandler(
    IOrderRepository orders,
    ILicenseRepository licenses,
    ICreditBalanceRepository creditBalances,
    ISubscriptionCheckout checkout,
    IUserContext user
) : IRequestHandler<ChangeSubscriptionCommand, PaymentResponse>
{
    public async Task<PaymentResponse> Handle(ChangeSubscriptionCommand request, CancellationToken cancellationToken)
    {
        ApplicationGuard.IsNull(request, Errors.InvalidRequest);

        var tenant = user.Tenant;

        // El reintento del mismo formulario va primero: para entonces la orden ya cambió el saldo a favor (regla 66).
        var stored = await orders.FindAsync<OrderAggregate>(request.Id, cancellationToken);

        if (stored is not null)
        {
            ApplicationGuard.IsTrue(stored.TenantDetail.Id != tenant, Errors.SubscriptionOrderBelongsToAnotherTenant);

            return PaymentResponses.Of(stored);
        }

        var subscription = SubscriptionTimeline.From(await orders.FindPaidByTenantAsync(tenant, cancellationToken));

        ApplicationGuard.IsNull(subscription, Errors.SubscriptionNotFound);

        var awaiting = await orders.FindAwaitingSubscriptionPaymentAsync(tenant, cancellationToken);
        var pending = awaiting.FirstOrDefault(x => x.Type == OrderType.Change && x.License.Id == request.LicenseId);

        if (pending is not null)
        {
            return PaymentResponses.Of(pending);
        }

        var target = await licenses.FindAsync<LicenseAggregate>(request.LicenseId, cancellationToken);

        ApplicationGuard.IsNull(target, Errors.LicenseNotFound);
        ApplicationGuard.IsFalse(target!.IsOfferedTo(tenant), Errors.LicenseIsNotOffered);

        var now = SystemClock.Instance.GetCurrentInstant();
        var current = subscription.CurrentAt(now);
        var balance = await creditBalances.FindAsync<CreditBalanceAggregate>(tenant, tenant, cancellationToken);
        var available = SubscriptionCheckout.AvailableCreditBalance(balance, awaiting, current.License.Total.Currency);

        var quote = SubscriptionChangeQuote.For(subscription, target, available, now);

        ApplicationGuard.IsNull(quote, Errors.LicenseIsNotAnUpgrade);

        var order = OrderAggregate.CreateChange(
            request.Id,
            Guid.NewGuid(),
            SubscriptionSnapshots.Of(target, quote!.Price),
            SubscriptionSnapshots.Payer(current.Buyer, user.IdUser),
            current.TenantDetail,
            current.License.Id,
            quote.CreditedPeriods,
            quote.Charge,
            user.IdUser);

        return await checkout.StartAsync(order, awaiting, cancellationToken);
    }
}
