using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Subscriptions.Commands.PurchasePackage;

/// <summary>
/// Compra o amplía un paquete para la copropiedad autenticada (pendings/739): el precio completo del paquete, con lo no usado
/// de la línea que amplía y el saldo a favor como anticipo, y el cobro de lo que falte.
/// </summary>
/// <remarks>
/// Con licencia anual el paquete vence con la licencia; con mensual, dura un año. Solo con la licencia al día. Una compra de
/// paquete que espera su pago se reemplaza por la siguiente; no toca una renovación ni un cambio pendientes.
/// </remarks>
public class PurchasePackageCommandHandler(
    IOrderRepository orders,
    IPackageRepository packages,
    ICreditBalanceRepository creditBalances,
    ISubscriptionCheckout checkout,
    IUserContext user
) : IRequestHandler<PurchasePackageCommand, PaymentResponse>
{
    /// <inheritdoc/>
    public async Task<PaymentResponse> Handle(PurchasePackageCommand request, CancellationToken cancellationToken)
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

        var package = await packages.FindAsync<PackageAggregate>(request.PackageId, cancellationToken);

        ApplicationGuard.IsTrue(package is null || package.IsDeleted, Errors.PackageNotFound);

        var now = SystemClock.Instance.GetCurrentInstant();
        var current = state!.Subscription.CurrentAt(now);
        var awaiting = await orders.FindAwaitingPackagePaymentAsync(tenant, cancellationToken);
        var balance = await creditBalances.FindAsync<CreditBalanceAggregate>(tenant, tenant, cancellationToken);
        var available = SubscriptionCheckout.AvailableCreditBalance(balance, awaiting, current.License.Total.Currency);

        var quote = state.QuoteAddOn(package!, request.Selections, available, now);

        var order = OrderAggregate.CreatePackagePurchase(
            request.Id,
            Guid.NewGuid(),
            state.ReferenceLicense(now),
            SubscriptionSnapshots.Payer(current.Buyer, user.IdUser),
            current.TenantDetail,
            quote.Quote.ToLine(Guid.NewGuid(), quote.ReplacesLineId),
            quote.CreditedPeriods,
            quote.Charge,
            user.IdUser);

        order.RecordAcceptedTerms(request.AcceptedTermsVersion);

        return await checkout.StartAsync(order, awaiting, cancellationToken);
    }
}
