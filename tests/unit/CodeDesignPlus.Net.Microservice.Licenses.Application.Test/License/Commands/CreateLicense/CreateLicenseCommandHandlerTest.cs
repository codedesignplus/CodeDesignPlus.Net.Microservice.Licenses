using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.CreateLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Moq;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License.Commands.CreateLicense
{
    public class CreateLicenseCommandHandlerTest
    {
        private readonly Mock<ILicenseRepository> repositoryMock;
        private readonly Mock<IUserContext> userContextMock;
        private readonly Mock<IPubSub> pubSubMock;
        private readonly Mock<IMapper> mapperMock;
        private readonly CreateLicenseCommandHandler handler;

        private readonly Price PriceMonthly = Price.Create(BillingTypeEnum.Monthly, Currency.Create("United States Dollar", "USD", "$"), 100, BillingModel.FlatRate, 0);
        private readonly Price PriceAnnualy = Price.Create(BillingTypeEnum.Annualy, Currency.Create("United States Dollar", "USD", "$"), 1000, BillingModel.FlatRate, 0);

        public CreateLicenseCommandHandlerTest()
        {
            repositoryMock = new Mock<ILicenseRepository>();
            userContextMock = new Mock<IUserContext>();
            pubSubMock = new Mock<IPubSub>();
            mapperMock = new Mock<IMapper>();
            handler = new CreateLicenseCommandHandler(repositoryMock.Object, userContextMock.Object, pubSubMock.Object, mapperMock.Object);
        }

        [Fact]
        public async Task Handle_RequestIsNull_ThrowsCodeDesignPlusException()
        {
            // Arrange
            CreateLicenseCommand request = null!;
            var cancellationToken = CancellationToken.None;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => handler.Handle(request, cancellationToken));

            Assert.Equal(Errors.InvalidRequest.GetCode(), exception.Code);
            Assert.Equal(Errors.InvalidRequest.GetMessage(), exception.Message);
            Assert.Equal(Layer.Application, exception.Layer);
        }

        [Fact]
        public async Task Handle_LicenseAlreadyExists_ThrowsCodeDesignPlusException()
        {
            // Arrange
            var request = new CreateLicenseCommand(Guid.NewGuid(), "Test License", "Short Description", "Test Description", [], [PriceMonthly, PriceAnnualy], Icon.Create("icon", "#FFFFFF"), "Test Terms of Service", [], true, false);
            var cancellationToken = CancellationToken.None;

            repositoryMock.Setup(x => x.ExistsAsync<LicenseAggregate>(request.Id, cancellationToken)).ReturnsAsync(true);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => handler.Handle(request, cancellationToken));

            Assert.Equal(Errors.LicenseAlreadyExists.GetCode(), exception.Code);
            Assert.Equal(Errors.LicenseAlreadyExists.GetMessage(), exception.Message);
            Assert.Equal(Layer.Application, exception.Layer);
        }

        [Fact]
        public async Task Handle_ValidRequest_CreatesLicenseAndPublishesEvents()
        {
            // Arrange
            var request = new CreateLicenseCommand(Guid.NewGuid(), "Test License", "Short Description", "Test Description", [], [PriceMonthly, PriceAnnualy], Icon.Create("icon", "#FFFFFF"), "Test Terms of Service", [], true, false);
            var cancellationToken = CancellationToken.None;
            var userId = Guid.NewGuid();

            repositoryMock.Setup(x => x.ExistsAsync<LicenseAggregate>(request.Id, cancellationToken)).ReturnsAsync(false);
            mapperMock.Setup(x => x.Map<List<ModuleEntity>>(request.Modules)).Returns([]);
            userContextMock.Setup(x => x.IdUser).Returns(userId);

            // Act
            await handler.Handle(request, cancellationToken);

            // Assert
            repositoryMock.Verify(x => x.CreateAsync(It.IsAny<LicenseAggregate>(), cancellationToken), Times.Once);
            pubSubMock.Verify(x => x.PublishAsync(It.IsAny<List<LicenseCreatedDomainEvent>>(), cancellationToken), Times.AtMostOnce);
        }
    }
}
