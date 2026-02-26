using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.RemoveModule;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.DomainEvents;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Entities;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Moq;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License.Commands.RemoveModule
{
    public class RemoveModuleCommandHandlerTest
    {
        private readonly Mock<ILicenseRepository> repositoryMock;
        private readonly Mock<IUserContext> userContextMock;
        private readonly Mock<IPubSub> pubSubMock;
        private readonly RemoveModuleCommandHandler handler;

        private readonly Price PriceMonthly = Price.Create(BillingTypeEnum.Monthly, Money.FromLong(100, "USD", 2), BillingModel.FlatRate, 0, 19);
        private readonly Price PriceAnnualy = Price.Create(BillingTypeEnum.Monthly, Money.FromLong(100, "USD", 2), BillingModel.FlatRate, 0, 19);

        public RemoveModuleCommandHandlerTest()
        {
            repositoryMock = new Mock<ILicenseRepository>();
            userContextMock = new Mock<IUserContext>();
            pubSubMock = new Mock<IPubSub>();
            handler = new RemoveModuleCommandHandler(repositoryMock.Object, userContextMock.Object, pubSubMock.Object);
        }

        [Fact]
        public async Task Handle_RequestIsNull_ThrowsCodeDesignPlusException()
        {
            // Arrange
            RemoveModuleCommand request = null!;
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
            var request = new RemoveModuleCommand(Guid.NewGuid(), Guid.NewGuid());
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
        public async Task Handle_ValidRequest_RemovesModuleAndPublishesEvents()
        {
            // Arrange
            var request = new RemoveModuleCommand(Guid.NewGuid(), Guid.NewGuid());
            var cancellationToken = CancellationToken.None;
            var idAggregate = Guid.NewGuid();
            var module = new ModuleEntity
            {
                Id = request.IdModule,
                Name = "Test Module",
                Description = "Test Module Description"
            };

            var license = LicenseAggregate.Create(idAggregate, "Test License", "Short Description", "Test Description", [module], [PriceAnnualy, PriceMonthly], Icon.Create("icon", "#FFFFFF"), "Test Terms of Service", [], true, false, false, Guid.NewGuid());

            repositoryMock
                .Setup(r => r.FindAsync<LicenseAggregate>(request.Id, cancellationToken))
                .ReturnsAsync(license);

            userContextMock.Setup(u => u.IdUser).Returns(Guid.NewGuid());

            // Act
            await handler.Handle(request, cancellationToken);

            // Assert
            Assert.Empty(license.Modules);
            repositoryMock.Verify(r => r.UpdateAsync(license, cancellationToken), Times.Once);
            pubSubMock.Verify(p => p.PublishAsync(It.IsAny<List<LicenseModuleRemovedDomainEvent>>(), cancellationToken), Times.AtMostOnce);
        }
    }
}
