using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Services.Notification;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Options;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order;
using CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Consumers;
using CodeDesignPlus.Net.Microservice.Licenses.Domain;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Repositories;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.AsyncWorker.Test.Consumers;

/// <summary>
/// El aviso de una orden pagada que no se aplicó (Kappali pendings/737, paso 6): va a la campana del rol de la plataforma,
/// sin tenant, con el tenant, el tipo, lo pagado y el motivo leídos de la orden, y no se repite si el evento llega dos veces.
/// </summary>
public class NotifyOrderNotAppliedHandlerTest
{
    private static readonly Guid PlatformRole = Guid.Parse("20000000-0000-0000-0000-000000000010");

    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<IInboxGrpc> inbox = new();
    private readonly List<NotificationRequest> notifications = [];
    private readonly OrderAggregate order = TestOrders.PaidFrom(Instant.FromUtc(2026, 11, 3, 15, 0), TestOrders.OrderId);

    public NotifyOrderNotAppliedHandlerTest()
    {
        inbox.Setup(x => x.NotifyAsync(It.IsAny<NotificationRequest>(), It.IsAny<CancellationToken>()))
            .Callback<NotificationRequest, CancellationToken>((request, _) => notifications.Add(request))
            .Returns(Task.CompletedTask);
        orders.Setup(x => x.FindAsync<OrderAggregate>(TestOrders.OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(order);
    }

    private NotifyOrderNotAppliedHandler Handler(Guid? role = null) => new(
        orders.Object, inbox.Object,
        Microsoft.Extensions.Options.Options.Create(new PlatformNoticeOptions { PlatformAdministratorRole = role ?? PlatformRole }),
        new Mock<ILogger<NotifyOrderNotAppliedHandler>>().Object);

    private static OrderProvisioningFailedDomainEvent Failed(Guid? orderId = null) => new(orderId ?? TestOrders.OrderId, "LicenseApplication", "105: The license name is not valid.", TestOrders.BuyerId, eventId: Guid.NewGuid(), occurredAt: Instant.FromUtc(2026, 11, 3, 15, 5));

    /// <summary>
    /// La campana va al rol de la plataforma, sin tenant, con el tipo de aviso y llevando a la orden.
    /// </summary>
    [Fact]
    public async Task HandleAsync_Failed_NotifiesThePlatformRoleWithoutTenant()
    {
        // Act
        await Handler().HandleAsync(Failed(), CancellationToken.None);

        // Assert
        var notification = Assert.Single(notifications);
        Assert.Equal(
            (PlatformNotificationKinds.LicenseOrderNotApplied, AudienceKind.Role, PlatformRole.ToString(), Guid.Empty.ToString(), "orders", TestOrders.OrderId.ToString()),
            (notification.Kind, notification.Audience.Kind, Assert.Single(notification.Audience.Values), notification.Tenant, notification.Resource.Module, notification.Resource.AggregateId));
    }

    /// <summary>
    /// El texto dice el tenant, el tipo de orden y el motivo, sin el monto: el importe lo formatea el frontend desde el
    /// payload y ya lo pinta en su propia línea.
    /// </summary>
    [Fact]
    public async Task HandleAsync_Failed_SaysTheTenantTheTypeAndTheReasonWithoutTheAmount()
    {
        // Act
        await Handler().HandleAsync(Failed(), CancellationToken.None);

        // Assert
        Assert.Equal(
            "La orden de compra de Conjunto Prueba se pagó, pero no se aplicó (paso LicenseApplication): 105: The license name is not valid. Revisa la orden.",
            notifications[0].Body);
    }

    /// <summary>
    /// El texto no lleva el monto en ninguna forma: ni la cifra en unidad menor ni la formateada ni el símbolo.
    /// </summary>
    [Fact]
    public async Task HandleAsync_Failed_BodyDoesNotCarryTheAmount()
    {
        // Act
        await Handler().HandleAsync(Failed(), CancellationToken.None);

        // Assert
        var body = notifications[0].Body;
        Assert.False(
            body.Contains("142.800", StringComparison.Ordinal)
            || body.Contains("142800", StringComparison.Ordinal)
            || body.Contains("14280000", StringComparison.Ordinal)
            || body.Contains('$', StringComparison.Ordinal),
            body);
    }

    /// <summary>
    /// El payload lleva lo que el frontend pinta: la orden, el tenant, el tipo, lo pagado y el motivo.
    /// </summary>
    [Fact]
    public async Task HandleAsync_Failed_SendsTheOrderInThePayload()
    {
        // Act
        await Handler().HandleAsync(Failed(), CancellationToken.None);

        // Assert
        var payload = notifications[0].JsonPayload;
        Assert.True(
            payload.Contains($"\"tenantId\":\"{TestOrders.TenantId}\"", StringComparison.Ordinal)
            && payload.Contains("\"amountPaid\":14280000", StringComparison.Ordinal)
            && payload.Contains("\"currency\":\"COP\"", StringComparison.Ordinal)
            && payload.Contains("\"orderType\":0", StringComparison.Ordinal)
            && payload.Contains("\"stepName\":\"LicenseApplication\"", StringComparison.Ordinal),
            payload);
    }

    /// <summary>
    /// Si la orden no aparece, el aviso sale igual con lo que trae el evento.
    /// </summary>
    [Fact]
    public async Task HandleAsync_OrderNotFound_StillNotifies()
    {
        // Arrange
        var missing = Guid.NewGuid();

        // Act
        await Handler().HandleAsync(Failed(missing), CancellationToken.None);

        // Assert
        Assert.Contains(missing.ToString(), Assert.Single(notifications).Body, StringComparison.Ordinal);
    }

    /// <summary>
    /// El mismo evento que llega dos veces da el mismo id de aviso: la bandeja no lo repite.
    /// </summary>
    [Fact]
    public async Task HandleAsync_TheSameEventTwice_SendsTheSameId()
    {
        // Arrange
        var @event = Failed();

        // Act
        await Handler().HandleAsync(@event, CancellationToken.None);
        await Handler().HandleAsync(@event, CancellationToken.None);

        // Assert
        Assert.Equal(notifications[0].Id, notifications[1].Id);
    }

    /// <summary>
    /// Sin el rol configurado el aviso no llegaría a nadie: falla para que se reintente y se vea.
    /// </summary>
    [Fact]
    public async Task HandleAsync_WithoutRole_Throws()
    {
        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => Handler(Guid.Empty).HandleAsync(Failed(), CancellationToken.None));
    }
}
