using System;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Cache.Abstractions;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetLicenseById;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using Moq;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License.Queries.GetLicenseById
{
    public class GetLicenseByIdQueryHandlerTest
    {
        private readonly Mock<ILicenseRepository> repositoryMock;
        private readonly Mock<IMapper> mapperMock;
        private readonly Mock<ICacheManager> cacheManagerMock;
        private readonly GetLicenseByIdQueryHandler handler;


        private readonly Price PriceMonthly = Price.Create(BillingTypeEnum.Monthly, Currency.Create(Guid.NewGuid(), "United States Dollar", "USD", "$"), 100, BillingModel.FlatRate, 0, 19);
        private readonly Price PriceAnnualy = Price.Create(BillingTypeEnum.Monthly, Currency.Create(Guid.NewGuid(), "United States Dollar", "USD", "$"), 100, BillingModel.FlatRate, 0, 19);


        public GetLicenseByIdQueryHandlerTest()
        {
            repositoryMock = new Mock<ILicenseRepository>();
            mapperMock = new Mock<IMapper>();
            cacheManagerMock = new Mock<ICacheManager>();
            handler = new GetLicenseByIdQueryHandler(repositoryMock.Object, mapperMock.Object, cacheManagerMock.Object);
        }

        [Fact]
        public async Task Handle_RequestIsNull_ThrowsCodeDesignPlusException()
        {
            // Arrange
            GetLicenseByIdQuery request = null!;

            // Act & Assert
            var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => handler.Handle(request, CancellationToken.None));

            Assert.Equal(Errors.InvalidRequest.GetCode(), exception.Code);
            Assert.Equal(Errors.InvalidRequest.GetMessage(), exception.Message);
            Assert.Equal(Layer.Application, exception.Layer);
        }

        [Fact]
        public async Task Handle_LicenseExistsInCache_ReturnsLicenseFromCache()
        {
            // Arrange
            var request = new GetLicenseByIdQuery(Guid.NewGuid());
            var licenseDto = new LicenseDto()
            {
                Id = request.Id,
                Name = "Test License",
                Description = "Test Description",
                Modules = [],
                Prices = [PriceMonthly, PriceAnnualy],
            };
            cacheManagerMock.Setup(x => x.ExistsAsync(request.Id.ToString())).ReturnsAsync(true);
            cacheManagerMock.Setup(x => x.GetAsync<LicenseDto>(request.Id.ToString())).ReturnsAsync(licenseDto);

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Equal(licenseDto, result);
            cacheManagerMock.Verify(x => x.ExistsAsync(request.Id.ToString()), Times.Once);
            cacheManagerMock.Verify(x => x.GetAsync<LicenseDto>(request.Id.ToString()), Times.Once);
            repositoryMock.Verify(x => x.FindAsync<LicenseAggregate>(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task Handle_LicenseNotInCache_ReturnsLicenseFromRepository()
        {
            // Arrange
            var request = new GetLicenseByIdQuery(Guid.NewGuid());
            var license = LicenseAggregate.Create(request.Id, "Test License", "Short Description","Test Description", [], [PriceAnnualy, PriceMonthly], Icon.Create("icon", "#FFFFFF"), "Term of Service", [], true, false, Guid.NewGuid());
            var licenseDto = new LicenseDto()
            {
                Id = license.Id,
                Name = license.Name,
                Description = license.Description,
                Modules = [],
                Prices = license.Prices,
                Icon = license.Icon,
                TermsOfService = license.TermsOfService,
                Attributes = license.Attributes,
                IsActive = license.IsActive
            };
            cacheManagerMock.Setup(x => x.ExistsAsync(request.Id.ToString())).ReturnsAsync(false);
            repositoryMock.Setup(x => x.FindAsync<LicenseAggregate>(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync(license);
            mapperMock.Setup(x => x.Map<LicenseDto>(license)).Returns(licenseDto);

            // Act
            var result = await handler.Handle(request, CancellationToken.None);

            // Assert
            Assert.Equal(licenseDto, result);
            cacheManagerMock.Verify(x => x.SetAsync(request.Id.ToString(), license, It.IsAny<TimeSpan?>()), Times.Once);
        }

        [Fact]
        public async Task Handle_LicenseNotFoundInRepository_ThrowsCodeDesignPlusException()
        {
            // Arrange
            var request = new GetLicenseByIdQuery(Guid.NewGuid());
            cacheManagerMock.Setup(x => x.ExistsAsync(request.Id.ToString())).ReturnsAsync(false);
            repositoryMock.Setup(x => x.FindAsync<LicenseAggregate>(request.Id, It.IsAny<CancellationToken>())).ReturnsAsync((LicenseAggregate)null!);

            // Act & Assert
            var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => handler.Handle(request, CancellationToken.None));

            Assert.Equal(Errors.LicenseNotFound.GetCode(), exception.Code);
            Assert.Equal(Errors.LicenseNotFound.GetMessage(), exception.Message);
            Assert.Equal(Layer.Application, exception.Layer);
        }
    }
}
