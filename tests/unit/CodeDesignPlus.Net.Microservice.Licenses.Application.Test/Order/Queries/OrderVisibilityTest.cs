using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.File.Storage.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetAnyOrderById;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetOrderById;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetOrderReceiptUrl;
using CodeDesignPlus.Net.Microservice.Licenses.Application.Setup;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.Order.Queries;

/// <summary>
/// Quién lee una orden por su id (pendings/735): la copropiedad de la sesión y quien la creó; la de otra copropiedad responde
/// lo mismo que una que no existe. La plataforma la lee por su propio endpoint.
/// </summary>
public class OrderVisibilityTest
{
    private static readonly Guid OtherTenant = Guid.Parse("ee000000-0000-4000-8000-000000000201");
    private static readonly Guid OtherUser = Guid.Parse("ee000000-0000-4000-8000-000000000202");

    private readonly Mock<IOrderRepository> orders = new();
    private readonly Mock<IUserContext> user = new();
    private readonly OrderAggregate order = TestOrders.Build();

    public OrderVisibilityTest()
    {
        MapsterConfigLicense.Configure();

        orders.Setup(x => x.FindAsync<OrderAggregate>(order.Id, It.IsAny<CancellationToken>())).ReturnsAsync(order);
    }

    private void SessionOf(Guid tenant, Guid userId)
    {
        user.SetupGet(x => x.Tenant).Returns(tenant);
        user.SetupGet(x => x.IdUser).Returns(userId);
    }

    private GetOrderByIdQueryHandler Handler() => new(orders.Object, new Mapper(), user.Object);

    [Fact]
    public async Task GetOrderById_OrderOfAnotherTenant_AnswersOrderNotFound()
    {
        // Arrange
        SessionOf(OtherTenant, OtherUser);

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => Handler().Handle(new GetOrderByIdQuery(order.Id), CancellationToken.None));

        // Assert
        Assert.Equal(Errors.OrderNotFound.GetCode(), exception.Code);
    }

    [Fact]
    public async Task GetOrderById_OrderThatDoesNotExist_AnswersTheSameOrderNotFound()
    {
        // Arrange
        SessionOf(OtherTenant, OtherUser);

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => Handler().Handle(new GetOrderByIdQuery(Guid.NewGuid()), CancellationToken.None));

        // Assert
        Assert.Equal(Errors.OrderNotFound.GetCode(), exception.Code);
    }

    [Fact]
    public async Task GetOrderById_OrderOfTheSessionTenant_ReturnsIt()
    {
        // Arrange
        SessionOf(TestOrders.TenantId, OtherUser);

        // Act
        var dto = await Handler().Handle(new GetOrderByIdQuery(order.Id), CancellationToken.None);

        // Assert
        Assert.Equal(order.Id, dto.Id);
    }

    /// <summary>
    /// La pantalla de compra lee la orden antes de que exista la copropiedad que crea, con el token de otra o de ninguna.
    /// </summary>
    [Fact]
    public async Task GetOrderById_PurchaseReadByItsBuyerFromAnotherTenant_ReturnsIt()
    {
        // Arrange
        SessionOf(Guid.Empty, TestOrders.BuyerId);

        // Act
        var dto = await Handler().Handle(new GetOrderByIdQuery(order.Id), CancellationToken.None);

        // Assert
        Assert.Equal(order.Id, dto.Id);
    }

    [Fact]
    public async Task GetAnyOrderById_OrderOfAnotherTenant_ReturnsIt()
    {
        // Arrange
        SessionOf(OtherTenant, OtherUser);

        // Act
        var dto = await new GetAnyOrderByIdQueryHandler(orders.Object, new Mapper()).Handle(new GetAnyOrderByIdQuery(order.Id), CancellationToken.None);

        // Assert
        Assert.Equal(order.Id, dto.Id);
    }

    [Fact]
    public async Task GetOrderReceiptUrl_OrderOfAnotherTenant_AnswersOrderNotFoundWithoutSigning()
    {
        // Arrange
        SessionOf(OtherTenant, OtherUser);
        var storage = new Mock<IFileStorage>();
        var handler = new GetOrderReceiptUrlQueryHandler(orders.Object, storage.Object, user.Object);

        // Act
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => handler.Handle(new GetOrderReceiptUrlQuery(order.Id), CancellationToken.None));

        // Assert
        Assert.Equal(Errors.OrderNotFound.GetCode(), exception.Code);
    }
}
