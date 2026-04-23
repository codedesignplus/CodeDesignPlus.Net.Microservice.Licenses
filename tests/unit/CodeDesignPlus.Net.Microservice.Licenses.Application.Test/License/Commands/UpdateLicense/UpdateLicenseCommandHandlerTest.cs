using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Cache.Abstractions;
using CodeDesignPlus.Net.gRpc.Clients.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Moq;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License.Commands.UpdateLicense
{
    public class UpdateLicenseCommandHandlerTest
    {
        private readonly Mock<ILicenseRepository> repositoryMock;
        private readonly Mock<IUserContext> userContextMock;
        private readonly Mock<IPubSub> pubSubMock;
        private readonly Mock<IMapper> mapperMock;
        private readonly Mock<ICurrencyGrpc> currencyGrpcMock;
        private readonly UpdateLicenseCommandHandler handler;


        private readonly Price PriceMonthlyVO = Price.Create(BillingType.Monthly, Money.FromDecimal( 100, "USD", 2), BillingModel.FlatRate, 0, 19);
        private readonly Price PriceAnnualyVO = Price.Create(BillingType.Annually, Money.FromDecimal( 100, "USD", 2), BillingModel.FlatRate, 0, 19);

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
        
        public UpdateLicenseCommandHandlerTest()
        {
            repositoryMock = new Mock<ILicenseRepository>();
            userContextMock = new Mock<IUserContext>();
            pubSubMock = new Mock<IPubSub>();
            mapperMock = new Mock<IMapper>();
            currencyGrpcMock = new Mock<ICurrencyGrpc>();

            handler = new UpdateLicenseCommandHandler(
                repositoryMock.Object,
                userContextMock.Object,
                pubSubMock.Object,
                mapperMock.Object,
                Mock.Of<ICacheManager>(),
                currencyGrpcMock.Object
            );
        }

        [Fact]
        public async Task Handle_RequestIsNull_ThrowsCodeDesignPlusException()
        {
            // Arrange
            UpdateLicenseCommand request = null!;
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
            var request = new UpdateLicenseCommand(Guid.NewGuid(), "Test License", "Short Description", "Test Description", [], [PriceMonthly, PriceAnnualy], Icon.Create("icon", "#FFFFFF"), "Test Terms of Service", [], true, false, false);
            var cancellationToken = CancellationToken.None;

            repositoryMock
                .Setup(repo => repo.FindAsync<LicenseAggregate>(request.Id, cancellationToken))
                .ReturnsAsync((LicenseAggregate)null!);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => handler.Handle(request, cancellationToken));

            Assert.Equal(Errors.LicenseNotFound.GetCode(), exception.Code);
            Assert.Equal(Errors.LicenseNotFound.GetMessage(), exception.Message);
            Assert.Equal(Layer.Application, exception.Layer);
        }

        [Fact]
        public async Task Handle_ValidRequest_UpdatesLicense()
        {
            // Arrange
            var license = LicenseAggregate.Create(Guid.NewGuid(), "Test License", "Short Description", "Test Description", [], [PriceMonthlyVO, PriceAnnualyVO], Icon.Create("icon", "#FFFFFF"), "Test Terms of Service", [], true, false, false, Guid.NewGuid());
            var request = new UpdateLicenseCommand(license.Id, "Test New License", "Short Description", "Test New Description", [], [PriceMonthly, PriceAnnualy], Icon.Create("icon", "#FFFFFF"), "Test New Terms of Service", [], true, false, false);
            var cancellationToken = CancellationToken.None;

            repositoryMock
                .Setup(repo => repo.FindAsync<LicenseAggregate>(request.Id, cancellationToken))
                .ReturnsAsync(license);

            mapperMock
                .Setup(mapper => mapper.Map<List<ModuleEntity>>(request.Modules))
                .Returns([]);

            userContextMock.Setup(user => user.IdUser)
                .Returns(Guid.NewGuid());

            // Act
            await handler.Handle(request, cancellationToken);

            // Assert
            repositoryMock.Verify(repo => repo.UpdateAsync(license, cancellationToken), Times.Once);
            pubSubMock.Verify(pubsub => pubsub.PublishAsync(It.IsAny<List<LicenseUpdatedDomainEvent>>(), cancellationToken), Times.AtMostOnce);
        }
    }
}
