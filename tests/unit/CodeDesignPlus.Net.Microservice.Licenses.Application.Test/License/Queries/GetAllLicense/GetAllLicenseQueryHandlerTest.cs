

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetAllLicense;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.Enums;
using CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License.Queries.GetAllLicense;

public class GetAAllLicenseQueryHandlerTest
{
    private readonly Mock<ILicenseRepository> repositoryMock;
    private readonly Mock<IMapper> mapperMock;
    private readonly GetAllLicenseQueryHandler handler;

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
        var license = LicenseAggregate.Create(Guid.NewGuid(), "Test License", "Test Description", [], BillingTypeEnum.Monthly, Currency.Create("United States Dollar", "USD", "$"), 100, [], Guid.NewGuid());
        var licenseDto = new LicenseDto()
        {
            Id = license.Id,
            Name = license.Name,
            Description = license.Description,
            Modules = [.. license.Modules.Select(module => new ModuleDto() { Id = module.Id, Name = module.Name })],
            BillingType = license.BillingType,
            Currency = license.Currency,
            Price = license.Price,
        };
        var licenses = new List<LicenseAggregate> { license };
        var licenseDtos = new List<LicenseDto> { licenseDto };

        repositoryMock
            .Setup(repo => repo.MatchingAsync<LicenseAggregate>(request.Criteria, cancellationToken))
            .ReturnsAsync(licenses);
        mapperMock
            .Setup(mapper => mapper.Map<List<LicenseDto>>(licenses))
            .Returns(licenseDtos);

        // Act
        var result = await handler.Handle(request, cancellationToken);

        // Assert
        Assert.Equal(licenseDtos, result);
    }
}