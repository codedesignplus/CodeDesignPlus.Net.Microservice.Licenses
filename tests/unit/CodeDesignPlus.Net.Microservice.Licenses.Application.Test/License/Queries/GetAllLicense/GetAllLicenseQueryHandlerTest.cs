

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Core.Abstractions.Models.Pager;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetAllLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License.Queries.GetAllLicense;

public class GetAAllLicenseQueryHandlerTest
{
    private readonly Mock<ILicenseRepository> repositoryMock;
    private readonly Mock<IMapper> mapperMock;
    private readonly GetAllLicenseQueryHandler handler;

    private readonly Price PriceMonthly = Price.Create(BillingTypeEnum.Monthly, Currency.Create("United States Dollar", "USD", "$"), 100, BillingModel.FlatRate);
    private readonly Price PriceAnnualy = Price.Create(BillingTypeEnum.Annualy, Currency.Create("United States Dollar", "USD", "$"), 1000, BillingModel.FlatRate);


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
        var license = LicenseAggregate.Create(Guid.NewGuid(), "Test License", "Test Description", [], [PriceMonthly, PriceAnnualy], Guid.NewGuid(), "Test Terms of Service", [], true, Guid.NewGuid());
        var licenseDto = new LicenseDto()
        {
            Id = license.Id,
            Name = license.Name,
            Description = license.Description,
            Modules = [.. license.Modules.Select(module => new ModuleDto() { Id = module.Id, Name = module.Name })],
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