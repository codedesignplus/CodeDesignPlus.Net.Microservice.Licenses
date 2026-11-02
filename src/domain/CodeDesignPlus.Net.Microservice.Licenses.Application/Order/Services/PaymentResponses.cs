using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.DataTransferObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Services;

/// <summary>
/// La respuesta del inicio de un pago a partir de la orden guardada, para devolverla igual a un reintento del mismo
/// formulario (pendings/589, regla 66).
/// </summary>
public static class PaymentResponses
{
    /// <summary>
    /// La respuesta del cobro que se inició o, si aún no se anotó, la de un cobro cuyo desenlace hay que esperar.
    /// </summary>
    /// <param name="order">La orden guardada.</param>
    /// <returns>La respuesta.</returns>
    public static PaymentResponse Of(OrderAggregate order)
    {
        if (order.AmountDue is { Amount: 0 })
        {
            return WithoutPayment(order);
        }

        return order.InitiatedPayment is { } payment
            ? new PaymentResponse
            {
                Id = order.Id,
                PaymentId = order.PaymentId,
                Success = payment.Success,
                NextAction = (NextActionType)payment.NextAction,
                RedirectUrl = payment.RedirectUrl,
                Metadata = payment.Metadata,
            }
            : new PaymentResponse
            {
                Id = order.Id,
                PaymentId = order.PaymentId,
                Success = order.IsSuccess,
                NextAction = NextActionType.WaitConfirmation,
            };
    }

    /// <summary>
    /// La respuesta de una orden que se pagó entera con crédito y saldo a favor: no hay pasarela.
    /// </summary>
    /// <param name="order">La orden.</param>
    /// <returns>La respuesta.</returns>
    public static PaymentResponse WithoutPayment(OrderAggregate order) => new()
    {
        Id = order.Id,
        PaymentId = order.PaymentId,
        Success = true,
        NextAction = NextActionType.None,
        ConfirmedWithoutPayment = true,
    };
}
