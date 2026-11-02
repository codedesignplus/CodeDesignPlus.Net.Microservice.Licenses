using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.Payment;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;
using Microsoft.Extensions.Options;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// Inicia el pago de una renovación o de un cambio de licencia de una copropiedad que ya existe (pendings/735, 738).
/// </summary>
public interface ISubscriptionCheckout
{
    /// <summary>
    /// Guarda la orden, aparta el saldo a favor que aplica, reemplaza las órdenes de la copropiedad que esperaban su pago y
    /// pide el cobro a ms-payments en la cuenta de la plataforma; si no queda nada por cobrar, la confirma sin pasarela.
    /// </summary>
    /// <param name="order">La orden nueva.</param>
    /// <param name="awaiting">Las renovaciones y cambios de la copropiedad que esperan todavía su pago.</param>
    /// <param name="cancellationToken">Token de cancelación.</param>
    /// <returns>Lo mismo que responde el inicio del pago de una compra.</returns>
    Task<PaymentResponse> StartAsync(OrderAggregate order, IReadOnlyCollection<OrderAggregate> awaiting, CancellationToken cancellationToken);
}

/// <summary>
/// Inicia el pago de una renovación o de un cambio de licencia (pendings/735, 738).
/// </summary>
/// <remarks>
/// Una copropiedad tiene como mucho una renovación o un cambio esperando su pago: el que se pide después reemplaza al
/// anterior. El reemplazado deja libre el saldo a favor que había apartado y, si su enlace de pago llega a pagarse, lo
/// pagado queda como saldo a favor sin tocar la licencia (<see cref="ISubscriptionSettlement"/>). Así el administrador
/// nunca se queda bloqueado por un pago que abandonó, y el saldo a favor no se gasta dos veces.
/// </remarks>
public class SubscriptionCheckout(
    IOrderRepository orders,
    ICreditBalanceRepository creditBalances,
    IPaymentGrpc paymentGrpc,
    IMapper mapper,
    IMediator mediator,
    IUserContext user,
    IOptions<OnlinePaymentOptions> onlinePayment,
    ILogger<SubscriptionCheckout> logger
) : ISubscriptionCheckout
{
    /// <inheritdoc/>
    public async Task<PaymentResponse> StartAsync(OrderAggregate order, IReadOnlyCollection<OrderAggregate> awaiting, CancellationToken cancellationToken)
    {
        // Primero se reemplazan las que esperaban su pago, para que el saldo que habían apartado vuelva a estar libre: quien
        // llama ya lo contó como disponible (AvailableCreditBalance).
        foreach (var previous in awaiting.Where(x => x.Id != order.Id))
        {
            await SupersedeAsync(previous, order.Id, cancellationToken);
        }

        // El saldo se aparta antes de guardar la orden y con un movimiento derivado de ella: dos envíos del mismo
        // formulario apartan una sola vez, y si no alcanza no queda una orden huérfana.
        await ReserveCreditBalanceAsync(order, cancellationToken);

        if (!await orders.CreateIfAbsentAsync(order, cancellationToken))
        {
            return PaymentResponses.Of(await orders.FindAsync<OrderAggregate>(order.Id, cancellationToken) ?? order);
        }

        if (order.AmountToCharge.Amount == 0)
        {
            // El crédito y el saldo a favor pagan todo: se confirma por el mismo camino que un pago aprobado.
            await mediator.Send(new UpdateStateOrderCommand(order.Id, Domain.Enums.PaymentStatus.Succeeded), cancellationToken);

            return PaymentResponses.WithoutPayment(order);
        }

        var response = mapper.Map<PaymentResponse>(await paymentGrpc.InitiatePaymentAsync(BuildPaymentRequest(order), cancellationToken));

        response.Id = order.Id;

        // Para devolverlo igual a un reintento del mismo formulario (pendings/589).
        await orders.RecordInitiatedPaymentAsync(order.Id, new InitiatedPayment(response.Success, (int)response.NextAction, response.RedirectUrl, response.Metadata ?? []), cancellationToken);

        return response;
    }

    /// <summary>
    /// El saldo a favor que puede usar una orden nueva: el disponible más lo que tienen apartado las órdenes que va a
    /// reemplazar.
    /// </summary>
    /// <param name="balance">El saldo de la copropiedad, si tiene.</param>
    /// <param name="awaiting">Las órdenes que esperan su pago y que la nueva reemplazará.</param>
    /// <param name="currency">La moneda.</param>
    /// <returns>El saldo disponible.</returns>
    public static Money AvailableCreditBalance(CreditBalanceAggregate? balance, IEnumerable<OrderAggregate> awaiting, string currency) =>
        awaiting
            .Where(x => x.CreditBalanceApplied is { Amount: > 0 })
            .Aggregate(balance?.Balance ?? Money.Zero(currency), (sum, x) => sum + x.CreditBalanceApplied!);

    private async Task ReserveCreditBalanceAsync(OrderAggregate order, CancellationToken cancellationToken)
    {
        if (order.CreditBalanceApplied is not { Amount: > 0 } applied)
        {
            return;
        }

        var tenant = order.TenantDetail.Id;
        var movement = CreditMovement.ForOrder(order.Id, CreditMovementKind.AppliedToOrder, applied, SystemClock.Instance.GetCurrentInstant());

        await creditBalances.UpdateWithRetryAsync(
            async () => await creditBalances.FindAsync<CreditBalanceAggregate>(tenant, tenant, cancellationToken) ?? CreditBalanceAggregate.Create(tenant, applied.Currency),
            balance => balance.Apply(movement),
            cancellationToken);
    }

    private async Task SupersedeAsync(OrderAggregate previous, Guid replacedBy, CancellationToken cancellationToken)
    {
        previous.Supersede(replacedBy, user.IdUser);

        if (!await orders.SupersedeAsync(previous, cancellationToken))
        {
            return;
        }

        logger.LogInformation("Order {OrderId} of tenant {TenantId} was superseded by {ReplacedBy} before being paid.", previous.Id, previous.TenantDetail.Id, replacedBy);

        if (previous.CreditBalanceApplied is not { Amount: > 0 } reserved)
        {
            return;
        }

        var tenant = previous.TenantDetail.Id;
        var movement = CreditMovement.ForOrder(previous.Id, CreditMovementKind.Released, reserved, SystemClock.Instance.GetCurrentInstant());

        await creditBalances.UpdateWithRetryAsync(
            async () => await creditBalances.FindAsync<CreditBalanceAggregate>(tenant, tenant, cancellationToken) ?? CreditBalanceAggregate.Create(tenant, reserved.Currency),
            balance => balance.Apply(movement),
            cancellationToken);
    }

    private InitiatePaymentRequest BuildPaymentRequest(OrderAggregate order)
    {
        var request = mapper.Map<InitiatePaymentRequest>(order);

        request.Id = order.PaymentId.ToString();
        request.ReferenceId = order.Id.ToString();
        request.Module = Commands.PayOrder.PayOrderCommandHandler.MODULE;

        // Lo vende la plataforma: se cobra en su cuenta, y la pasarela devuelve al administrador a Configuración ›
        // Organización, donde pidió la renovación o el cambio (pendings/660, 735).
        request.PlatformAccount = true;
        request.ReturnUrl = onlinePayment.Value.SubscriptionReturnUrl;

        var (subTotal, tax) = Split(order.License, order.AmountToCharge);

        request.SubTotal = new Amount { Currency = subTotal.Currency, Value = subTotal.Amount };
        request.Tax = new Amount { Currency = tax.Currency, Value = tax.Amount };
        request.Total = new Amount { Currency = order.AmountToCharge.Currency, Value = order.AmountToCharge.Amount };

        request.Description = order.Type == OrderType.Renewal
            ? $"Renewal of license {order.License.Name} for tenant {order.TenantDetail.Name}."
            : $"Change to license {order.License.Name} for tenant {order.TenantDetail.Name}.";

        return request;
    }

    /// <summary>
    /// Reparte lo que se cobra entre base e impuestos en la misma proporción del precio, porque ms-payments exige que el
    /// total sea la base más los impuestos. Cobrando el precio completo, es el mismo reparto del precio.
    /// </summary>
    internal static (Money SubTotal, Money Tax) Split(Domain.ValueObjects.License license, Money due)
    {
        if (license.Total.Amount == 0 || due == license.Total)
        {
            return (Money.FromLong(due.Amount - Math.Min(license.Tax.Amount, due.Amount), due.Currency), Money.FromLong(Math.Min(license.Tax.Amount, due.Amount), due.Currency));
        }

        var tax = (long)((decimal)license.Tax.Amount * due.Amount / license.Total.Amount);

        return (Money.FromLong(due.Amount - tax, due.Currency), Money.FromLong(tax, due.Currency));
    }
}
