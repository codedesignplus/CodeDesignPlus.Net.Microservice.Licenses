using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ServiceDomain;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.RenewSubscription;

/// <summary>
/// Renueva la licencia de la copropiedad autenticada: crea la orden de renovación con la licencia vigente al precio
/// vigente del catálogo, aplica el saldo a favor y pide el cobro de lo que falte (pendings/735).
/// </summary>
/// <remarks>
/// El periodo no se fija aquí sino al aprobarse el pago: empieza donde termina lo ya pagado o, si ya venció, ese día.
/// Pulsar dos veces con el mismo id devuelve el mismo cobro; pedirla otra vez con otro id mientras la anterior espera su
/// pago devuelve el cobro de la anterior.
/// </remarks>
public class RenewSubscriptionCommandHandler(
    IOrderRepository orders,
    ILicenseRepository licenses,
    ICreditBalanceRepository creditBalances,
    ISubscriptionCheckout checkout,
    IUserContext user,
    IPackageRepository packages
) : IRequestHandler<RenewSubscriptionCommand, PaymentResponse>
{
    public async Task<PaymentResponse> Handle(RenewSubscriptionCommand request, CancellationToken cancellationToken)
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

        var state = await TenantPackages.LoadAsync(orders, tenant, cancellationToken);

        ApplicationGuard.IsNull(state, Errors.SubscriptionNotFound);

        var now = SystemClock.Instance.GetCurrentInstant();
        var current = state!.Subscription.CurrentAt(now);
        var awaiting = await orders.FindAwaitingSubscriptionPaymentAsync(tenant, cancellationToken);

        // Los paquetes que se renuevan con la licencia anual, al precio vigente (pendings/739).
        var renewed = await state.RenewalQuotesAsync(packages, request.Packages, now, cancellationToken);

        var pending = awaiting.FirstOrDefault(x => x.Type == OrderType.Renewal && x.License.Id == current.License.Id && RenewalQuote.SamePackages(x, renewed));

        if (pending is not null)
        {
            return PaymentResponses.Of(pending);
        }

        // Una licencia retirada de la venta se sigue renovando: retirarla es dejar de vendérsela a copropiedades nuevas.
        // Una borrada no, porque ya no tiene precio vigente.
        var catalog = await licenses.FindAsync<LicenseAggregate>(current.License.Id, cancellationToken);

        ApplicationGuard.IsTrue(catalog is null || catalog.IsDeleted, Errors.LicenseCannotBeRenewed);

        var price = catalog!.Prices.FirstOrDefault(x => x.BillingType == current.License.BillingType);

        ApplicationGuard.IsNull(price, Errors.LicenseCannotBeRenewed);

        var balance = await creditBalances.FindAsync<CreditBalanceAggregate>(tenant, tenant, cancellationToken);
        var currency = price!.Total.Currency;

        var lines = renewed.Select(x => x.Quote.ToLine(Guid.NewGuid(), x.Replaces)).ToList();
        var total = renewed.Aggregate(price.Total, (sum, x) => sum + x.Quote.Total);
        var charge = SubscriptionCharge.Compute(total, Money.Zero(currency), SubscriptionCheckout.AvailableCreditBalance(balance, awaiting, currency));

        var order = OrderAggregate.CreateRenewal(
            request.Id,
            Guid.NewGuid(),
            SubscriptionSnapshots.Renewal(current.License, price, catalog.InvoiceConcept),
            SubscriptionSnapshots.Payer(current.Buyer, user.IdUser),
            current.TenantDetail,
            charge,
            user.IdUser,
            lines);

        order.RecordAcceptedTerms(request.AcceptedTermsVersion);

        return await checkout.StartAsync(order, awaiting, cancellationToken);
    }
}
