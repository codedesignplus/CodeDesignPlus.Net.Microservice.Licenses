using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.DeleteLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Moq;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License.Commands.DeleteLicense
{
    public class DeleteLicenseCommandHandlerTest
    {
        private readonly Mock<ILicenseRepository> repositoryMock;
        private readonly Mock<IUserContext> userContextMock;
        private readonly Mock<IPubSub> pubSubMock;
        private readonly DeleteLicenseCommandHandler handler;

        private readonly Price PriceMonthly = Price.Create(BillingTypeEnum.Monthly, Currency.Create(Guid.NewGuid(), "United States Dollar", "USD", "$"), 100, BillingModel.FlatRate, 0, 19);
        private readonly Price PriceAnnualy = Price.Create(BillingTypeEnum.Monthly, Currency.Create(Guid.NewGuid(), "United States Dollar", "USD", "$"), 100, BillingModel.FlatRate, 0, 19);

        public DeleteLicenseCommandHandlerTest()
        {
            repositoryMock = new Mock<ILicenseRepository>();
            userContextMock = new Mock<IUserContext>();
            pubSubMock = new Mock<IPubSub>();
            handler = new DeleteLicenseCommandHandler(repositoryMock.Object, userContextMock.Object, pubSubMock.Object);
        }

        [Fact]
        public async Task Handle_RequestIsNull_ThrowsCodeDesignPlusException()
        {
            // Arrange
            DeleteLicenseCommand request = null!;
            var cancellationToken = CancellationToken.None;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => handler.Handle(request, cancellationToken));

            Assert.Equal(Errors.InvalidRequest.GetCode(), exception.Code);
            Assert.Equal(Errors.InvalidRequest.GetMessage(), exception.Message);
            Assert.Equal(Layer.Application, exception.Layer);
        }

        [Fact]
        public async Task Handle_LicenseNotFound_ThrowsCodeDesignPlusException()
        {
            // Arrange
            var request = new DeleteLicenseCommand(Guid.NewGuid());
            var cancellationToken = CancellationToken.None;

            repositoryMock
                .Setup(r => r.FindAsync<LicenseAggregate>(request.Id, cancellationToken))
                .ReturnsAsync((LicenseAggregate)null!);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => handler.Handle(request, cancellationToken));

            Assert.Equal(Errors.LicenseNotFound.GetCode(), exception.Code);
            Assert.Equal(Errors.LicenseNotFound.GetMessage(), exception.Message);
            Assert.Equal(Layer.Application, exception.Layer);
        }

        [Fact]
        public async Task Handle_ValidRequest_DeletesLicenseAndPublishesEvents()
        {
            // Arrange
            var request = new DeleteLicenseCommand(Guid.NewGuid());
            var cancellationToken = CancellationToken.None;
            var licenseAggregate = LicenseAggregate.Create(Guid.NewGuid(), "Test License", "Short Description", "Test Description", [], [PriceMonthly, PriceAnnualy], Icon.Create("icon", "#FFFFFF"), "Test Terms of Service", [], true, false, false, Guid.NewGuid());

            repositoryMock
                .Setup(r => r.FindAsync<LicenseAggregate>(request.Id, cancellationToken))
                .ReturnsAsync(licenseAggregate);

            userContextMock.Setup(u => u.IdUser).Returns(Guid.NewGuid());

            // Act
            await handler.Handle(request, cancellationToken);

            // Assert
            repositoryMock.Verify(r => r.DeleteAsync<LicenseAggregate>(licenseAggregate.Id, cancellationToken), Times.Once);
            pubSubMock.Verify(p => p.PublishAsync(It.IsAny<List<LicenseDeletedDomainEvent>>(), cancellationToken), Times.AtMostOnce);
        }
    }
}
