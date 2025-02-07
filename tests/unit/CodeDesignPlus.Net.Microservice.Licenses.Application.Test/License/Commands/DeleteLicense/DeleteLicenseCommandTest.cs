using System;
using FluentValidation.TestHelper;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.DeleteLicense;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License.Commands.DeleteLicense
{
    public class DeleteLicenseCommandTest
    {
        private readonly Validator validator;

        public DeleteLicenseCommandTest()
        {
            validator = new Validator();
        }

        [Fact]
        public void Should_Have_Error_When_Id_Is_Empty()
        {
            var command = new DeleteLicenseCommand(Guid.Empty);
            var result = validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void Should_Not_Have_Error_When_Id_Is_Valid()
        {
            var command = new DeleteLicenseCommand(Guid.NewGuid());
            var result = validator.TestValidate(command);
            result.ShouldNotHaveValidationErrorFor(x => x.Id);
        }
    }
}
