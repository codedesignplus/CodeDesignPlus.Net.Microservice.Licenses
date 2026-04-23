using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.CreateLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;
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
        private readonly Mock<ICurrencyGrpc> currencyGrpcMock;
        private readonly CreateLicenseCommandHandler handler;

        // private readonly Price PriceMonthly = Price.Create(BillingType.Monthly, Money.FromDecimal( 100, "USD", 2), BillingModel.FlatRate, 0, 19);
        // private readonly Price PriceAnnualy = Price.Create(BillingType.Monthly, Money.FromDecimal( 100, "USD", 2), BillingModel.FlatRate, 0, 19);

        private readonly PriceDto PriceMonthly = new()
        {
            BasePrice = 100,
            BillingModel = BillingModel.FlatRate,
            BillingType = BillingType.Monthly,
            Currency = "USD",
            DiscountPercentage = 0,
            TaxPercentage = 19
        };

        
        private readonly PriceDto PriceAnnualy = new()
        {
            BasePrice = 100,
            BillingModel = BillingModel.FlatRate,
            BillingType = BillingType.Annually,
            Currency = "USD",
            DiscountPercentage = 0,
            TaxPercentage = 19
        };

        public CreateLicenseCommandHandlerTest()
        {
            repositoryMock = new Mock<ILicenseRepository>();
            userContextMock = new Mock<IUserContext>();
            pubSubMock = new Mock<IPubSub>();
            mapperMock = new Mock<IMapper>();
            currencyGrpcMock = new Mock<ICurrencyGrpc>();

            handler = new CreateLicenseCommandHandler(repositoryMock.Object, userContextMock.Object, pubSubMock.Object, mapperMock.Object, currencyGrpcMock.Object);
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
            var request = new CreateLicenseCommand(Guid.NewGuid(), "Test License", "Short Description", "Test Description", [], [PriceMonthly, PriceAnnualy], Icon.Create("icon", "#FFFFFF"), "Test Terms of Service", [], true, false, false);
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
            var request = new CreateLicenseCommand(Guid.NewGuid(), "Test License", "Short Description", "Test Description", [], [PriceMonthly, PriceAnnualy], Icon.Create("icon", "#FFFFFF"), "Test Terms of Service", [], true, false, false);
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
