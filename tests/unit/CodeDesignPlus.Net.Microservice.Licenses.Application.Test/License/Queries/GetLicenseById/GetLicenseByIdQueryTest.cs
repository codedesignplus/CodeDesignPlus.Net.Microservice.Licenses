using System;
using FluentValidation.TestHelper;
using Xunit;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Queries.GetLicenseById;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License.Queries.GetLicenseById;

public class GetLicenseByIdQueryTest
{
    private readonly Validator validator;

    public GetLicenseByIdQueryTest()
    {
        validator = new Validator();
    }

    [Fact]
    public void Should_Have_Error_When_Id_Is_Empty()
    {
        var query = new GetLicenseByIdQuery(Guid.Empty);
        var result = validator.TestValidate(query);
        result.ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void Should_Not_Have_Error_When_Id_Is_Not_Empty()
    {
        var query = new GetLicenseByIdQuery(Guid.NewGuid());
        var result = validator.TestValidate(query);
        result.ShouldNotHaveValidationErrorFor(x => x.Id);
    }
}
