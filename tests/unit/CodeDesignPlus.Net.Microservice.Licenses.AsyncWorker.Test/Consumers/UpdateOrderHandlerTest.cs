using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.PayOrder;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Commands.UpdateStateOrder;
using CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Consumers;
using CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;
using MediatR;
using Xunit;
using GrpcPaymentStatus = CodeDesignPlus.Net.gRpc.Clients.Services.Payment.PaymentStatus;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Test.Consumers;

/// <summary>
/// El resultado de un pago que llega de ms-payments (pendings/738).
/// </summary>
public class UpdateOrderHandlerTest
{
    /// <summary>
    /// Un pago rechazado llega a la orden con su motivo y lo que dijo el banco: antes la orden quedaba con el error nulo y
    /// el motivo solo vivía en ms-payments.
    /// </summary>
    [Fact]
    public async Task HandleAsync_APaymentRejected_PassesTheReasonAndTheProviderMessageToTheOrder()
    {
        // Arrange
        var orderId = Guid.NewGuid();
        var mediator = new Mock<IMediator>();
        var orders = new Mock<IOrderRepository>();
        UpdateStateOrderCommand? sent = null;
        orders.Setup(x => x.ExistsAsync<OrderAggregate>(orderId, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        mediator.Setup(x => x.Send(It.IsAny<UpdateStateOrderCommand>(), It.IsAny<CancellationToken>()))
            .Callback<IRequest, CancellationToken>((command, _) => sent = (UpdateStateOrderCommand)command)
            .Returns(Task.CompletedTask);

        var @event = new PaymentResponseAssociatedDomainEvent(Guid.NewGuid(), PayOrderCommandHandler.MODULE, orderId, GrpcPaymentStatus.Failed, new Dictionary<string, string?>(), Guid.NewGuid(), failureReason: 1, providerMessage: "La transacción fue rechazada (Sandbox)");

        // Act
        await new UpdateOrderHandler(Mock.Of<ILogger<UpdateOrderHandler>>(), mediator.Object, orders.Object).HandleAsync(@event, CancellationToken.None);

        // Assert
        Assert.Equal((PaymentStatus.Failed, (PaymentFailureReason?)PaymentFailureReason.Declined, "La transacción fue rechazada (Sandbox)"), (sent!.PaymentStatus, sent.FailureReason, sent.ProviderMessage));
    }
}
