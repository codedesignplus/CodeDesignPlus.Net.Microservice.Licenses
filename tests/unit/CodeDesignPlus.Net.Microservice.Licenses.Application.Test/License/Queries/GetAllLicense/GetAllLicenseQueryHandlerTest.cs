

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetAllLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;
using CodeDesignPlus.Net.ValueObjects.Financial;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License.Queries.GetAllLicense;

public class GetAAllLicenseQueryHandlerTest
{
    private readonly Mock<ILicenseRepository> repositoryMock;
    private readonly Mock<IMapper> mapperMock;
    private readonly GetAllLicenseQueryHandler handler;

    
        private readonly PriceDto PriceMonthly = new()
        {
            BasePrice = 100,
            Currency = "USD",
            BillingType = BillingType.Monthly,
            BillingModel = BillingModel.FlatRate,
            DiscountPercentage = 0,
            TaxPercentage = 19
        };
    
        private readonly PriceDto PriceAnnualy = new()
        {
            BasePrice = 1000,
            Currency = "USD",
            BillingType = BillingType.Annually,
            BillingModel = BillingModel.FlatRate,
            DiscountPercentage = 0,
            TaxPercentage = 19
        };


    public GetAAllLicenseQueryHandlerTest()
    {
        repositoryMock = new Mock<ILicenseRepository>();
        mapperMock = new Mock<IMapper>();
        handler = new GetAllLicenseQueryHandler(repositoryMock.Object, mapperMock.Object);
    }

    [Fact]
    public async Task Handle_RequestIsNull_ThrowsCodeDesignPlusException()
    {
        // Arrange
        GetAllLicenseQuery request = null!;
        var cancellationToken = CancellationToken.None;

        // Act & Assert
        var exception = await Assert.ThrowsAsync<CodeDesignPlusException>(() => handler.Handle(request, cancellationToken));

        Assert.Equal(Errors.InvalidRequest.GetCode(), exception.Code);
        Assert.Equal(Errors.InvalidRequest.GetMessage(), exception.Message);
        Assert.Equal(Layer.Application, exception.Layer);
    }

    [Fact]
    public async Task Handle_ValidRequest_ReturnsLicenseDtoList()
    {
        // Arrange
        var request = new GetAllLicenseQuery(null!);
        var cancellationToken = CancellationToken.None;
        var license = LicenseAggregate.Create(
            Guid.NewGuid(), 
            "Test License", 
            "Short Description",
            "Test Description", 
            [], 
                [
                    Price.Create(PriceMonthly.BillingType, Money.FromDecimal(PriceMonthly.BasePrice, PriceMonthly.Currency, 2), PriceMonthly.BillingModel, PriceMonthly.DiscountPercentage, PriceMonthly.TaxPercentage), 
                    Price.Create(PriceAnnualy.BillingType, Money.FromDecimal(PriceAnnualy.BasePrice, PriceAnnualy.Currency, 2), PriceAnnualy.BillingModel, PriceAnnualy.DiscountPercentage, PriceAnnualy.TaxPercentage)
                ], 
            Icon.Create("icon", "#FFFFFF"), 
            "Test Terms of Service", 
                [], 
            true, 
            false, 
            false, 
            Guid.NewGuid()
        );
        var licenseDto = new LicenseDto()
        {
            Id = license.Id,
            Name = license.Name,
            Description = license.Description,
            Modules = [.. license.Modules.Select(module => new ModuleDto() { Id = module.Id, Name = module.Name, Description = module.Description })],
            Prices = license.Prices,
            TermsOfService = license.TermsOfService,
            IsActive = license.IsActive,
        };
        var licenses = new List<LicenseAggregate> { license };
        var licenseDtos = new List<LicenseDto> { licenseDto };
        var pagination = Pagination<LicenseAggregate>.Create(licenses, 1, 10, 0);

        repositoryMock
            .Setup(repo => repo.MatchingAsync<LicenseAggregate>(request.Criteria, cancellationToken))
            .ReturnsAsync(pagination);
        mapperMock
            .Setup(mapper => mapper.Map<Pagination<LicenseDto>>(pagination))
            .Returns(Pagination<LicenseDto>.Create(licenseDtos, 1, 10, 0));

        // Act
        var result = await handler.Handle(request, cancellationToken);

        // Assert
        Assert.Equal(licenseDtos, result.Data);
    }
}