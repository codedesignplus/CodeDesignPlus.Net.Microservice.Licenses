using CodeDesignPlus.Microservice.Api.Dtos;
using System;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.AddModule;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.CreateLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.DataTransferObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetAllLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetLicenseById;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.Microservice.Licenses.Rest.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Rest.Test.Controllers
{
    public class LicenseControllerTest
    {
        private readonly Mock<IMediator> mediatorMock;
        private readonly Mock<IMapper> mapperMock;
        private readonly LicenseController controller;

        public LicenseControllerTest()
        {
            mediatorMock = new Mock<IMediator>();
            mapperMock = new Mock<IMapper>();
            controller = new LicenseController(mediatorMock.Object, mapperMock.Object);
        }

        [Fact]
        public async Task GetLicenses_ReturnsOkResult()
        {
            // Arrange
            var criteria = new C.Criteria();
            mediatorMock
                .Setup(m => m.Send(It.IsAny<GetAllLicenseQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<LicenseDto>());

            // Act
            var result = await controller.GetLicenses(criteria, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.IsAssignableFrom<IEnumerable<LicenseDto>>(okResult.Value);
            mediatorMock.Verify(m => m.Send(It.IsAny<GetAllLicenseQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetLicenseById_ReturnsOkResult()
        {
            // Arrange
            var licenseId = Guid.NewGuid();
            mediatorMock
                .Setup(m => m.Send(It.IsAny<GetLicenseByIdQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new LicenseDto()
                {
                    Id = licenseId,
                    Name = "Test License",
                    Description = "Test Description",
                    Modules = [],
                    BillingType = BillingTypeEnum.Monthly,
                    Currency = Currency.Create("United States Dollar", "USD", "$"),
                    Price = 100,
                });

            // Act
            var result = await controller.GetLicenseById(licenseId, CancellationToken.None);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            Assert.IsType<LicenseDto>(okResult.Value);
            mediatorMock.Verify(m => m.Send(It.IsAny<GetLicenseByIdQuery>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task CreateLicense_ReturnsNoContentResult()
        {
            // Arrange
            var createLicenseDto = new CreateLicenseDto()
            {
                Name = "Test License",
                Description = "Test Description",
                Modules = [],
                BillingType = BillingTypeEnum.Monthly,
                Currency = Currency.Create("United States Dollar", "USD", "$"),
                Price = 100,
            };
            mapperMock
                .Setup(m => m.Map<CreateLicenseCommand>(It.IsAny<CreateLicenseDto>()))
                .Returns(new CreateLicenseCommand(Guid.NewGuid(), "Test License", "Test Description", [], BillingTypeEnum.Monthly, Currency.Create("United States Dollar", "USD", "$"), 100, []));

            // Act
            var result = await controller.CreateLicense(createLicenseDto, CancellationToken.None);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task AddModules_ReturnsNoContentResult()
        {
            // Arrange
            var licenseId = Guid.NewGuid();
            var addModuleDto = new AddModuleDto();
            mapperMock
                .Setup(m => m.Map<AddModuleCommand>(It.IsAny<AddModuleDto>()))
                .Returns(new AddModuleCommand(licenseId, Guid.NewGuid(), "Test Module"));

            // Act
            var result = await controller.AddModules(licenseId, addModuleDto, CancellationToken.None);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task UpdateLicense_ReturnsNoContentResult()
        {
            // Arrange
            var licenseId = Guid.NewGuid();
            var updateLicenseDto = new UpdateLicenseDto();
            mapperMock
                .Setup(m => m.Map<UpdateLicenseCommand>(It.IsAny<UpdateLicenseDto>()))
                .Returns(new UpdateLicenseCommand(licenseId, "Test License", "Test Description", [], BillingTypeEnum.Monthly, Currency.Create("United States Dollar", "USD", "$"), 100, []));

            // Act
            var result = await controller.UpdateLicense(licenseId, updateLicenseDto, CancellationToken.None);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task DeleteLicense_ReturnsNoContentResult()
        {
            // Arrange
            var licenseId = Guid.NewGuid();

            // Act
            var result = await controller.DeleteLicense(licenseId, CancellationToken.None);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }

        [Fact]
        public async Task RemoveModule_ReturnsNoContentResult()
        {
            // Arrange
            var licenseId = Guid.NewGuid();
            var moduleId = Guid.NewGuid();

            // Act
            var result = await controller.RemoveModule(licenseId, moduleId, CancellationToken.None);

            // Assert
            Assert.IsType<NoContentResult>(result);
        }
    }
}
