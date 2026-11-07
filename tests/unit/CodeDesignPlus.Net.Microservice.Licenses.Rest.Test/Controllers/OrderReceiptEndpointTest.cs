using CodeDesignPlus.Net.Microservice.Licenses.Application.Order.Queries.GetAnyOrderReceiptUrl;
using CodeDesignPlus.Net.Security.Abstractions;

namespace CodeDesignPlus.Net.Microservice.Licenses.Rest.Test.Controllers;

/// <summary>
/// The receipt of the Super Admin's order detail (pendings/674): the platform's view of <c>GET Order/{id}/receipt-url</c>.
/// </summary>
public class OrderReceiptEndpointTest
{
    private readonly Mock<IMediator> mediator = new();

    [Fact]
    public async Task GetAnyOrderReceiptUrl_OrderWithReceipt_ReturnsTheSignedUrl()
    {
        // Arrange
        var id = Guid.NewGuid();
        mediator.Setup(x => x.Send(It.Is<GetAnyOrderReceiptUrlQuery>(q => q.OrderId == id), It.IsAny<CancellationToken>())).ReturnsAsync("https://blob.test/receipt.pdf?sig=674");

        // Act
        var response = await Controller().GetAnyOrderReceiptUrl(id, CancellationToken.None);

        // Assert
        Assert.Equal("https://blob.test/receipt.pdf?sig=674", Assert.IsType<OkObjectResult>(response).Value);
    }

    [Fact]
    public async Task GetAnyOrderReceiptUrl_OrderWithoutReceipt_AnswersNoContent()
    {
        // Arrange
        mediator.Setup(x => x.Send(It.IsAny<GetAnyOrderReceiptUrlQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync((string?)null);

        // Act
        var response = await Controller().GetAnyOrderReceiptUrl(Guid.NewGuid(), CancellationToken.None);

        // Assert
        Assert.IsType<NoContentResult>(response);
    }

    private OrderController Controller() => new(mediator.Object, Mock.Of<IMapper>(), Mock.Of<IUserContext>());
}
