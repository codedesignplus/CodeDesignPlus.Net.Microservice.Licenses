using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.AddModule;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Moq;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License.Commands.AddModule
{
    public class AddModuleCommandHandlerTest
    {
        private readonly Mock<ILicenseRepository> repositoryMock;
        private readonly Mock<IUserContext> userContextMock;
        private readonly Mock<IPubSub> pubSubMock;
        private readonly AddModuleCommandHandler handler;

        public AddModuleCommandHandlerTest()
        {
            repositoryMock = new Mock<ILicenseRepository>();
            userContextMock = new Mock<IUserContext>();
            pubSubMock = new Mock<IPubSub>();
            handler = new AddModuleCommandHandler(repositoryMock.Object, userContextMock.Object, pubSubMock.Object);
        }

        [Fact]
        public async Task Handle_RequestIsNull_ThrowsInvalidRequestException()
        {
            // Arrange
            AddModuleCommand request = null!;
            var cancellationToken = CancellationToken.None;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => handler.Handle(request, cancellationToken));

            Assert.Equal(Errors.InvalidRequest.GetCode(), exception.Code);
            Assert.Equal(Errors.InvalidRequest.GetMessage(), exception.Message);
            Assert.Equal(Layer.Application, exception.Layer);
        }

        [Fact]
        public async Task Handle_LicenseNotFound_ThrowsLicenseNotFoundException()
        {
            // Arrange
            var request = new AddModuleCommand(Guid.NewGuid(), Guid.NewGuid(), "TestModule");
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
        public async Task Handle_ValidRequest_UpdatesLicenseAndPublishesEvents()
        {
            // Arrange
            var request = new AddModuleCommand(Guid.NewGuid(), Guid.NewGuid(), "TestModule");
            var cancellationToken = CancellationToken.None;
            var license = LicenseAggregate.Create(Guid.NewGuid(), "TestLicense", "TestDescription", [], Domain.Enums.BillingTypeEnum.Annualy, Currency.Create("MXN", "Mexican Peso", "$"), 100, [], Guid.NewGuid());

            repositoryMock
                .Setup(r => r.FindAsync<LicenseAggregate>(request.Id, cancellationToken))
                .ReturnsAsync(license);

            userContextMock.Setup(u => u.IdUser).Returns(Guid.NewGuid());

            // Act
            await handler.Handle(request, cancellationToken);

            // Assert
            repositoryMock.Verify(r => r.UpdateAsync(license, cancellationToken), Times.Once);
            pubSubMock.Verify(p => p.PublishAsync(It.IsAny<List<LicenseModuleAddedDomainEvent>>(), cancellationToken), Times.AtMostOnce);
        }
    }
}
