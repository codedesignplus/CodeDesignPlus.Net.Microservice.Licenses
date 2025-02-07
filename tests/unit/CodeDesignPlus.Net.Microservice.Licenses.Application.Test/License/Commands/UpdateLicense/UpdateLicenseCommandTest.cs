using System;
using Xunit;
using FluentValidation.TestHelper;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.UpdateLicense;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License.Commands.UpdateLicense
{
    public class UpdateLicenseCommandTest
    {
        private readonly Validator _validator;

        public UpdateLicenseCommandTest()
        {
            _validator = new Validator();
        }

        [Fact]
        public void Should_Have_Error_When_Id_Is_Empty()
        {
            var command = new UpdateLicenseCommand(Guid.Empty, "ValidName", "ValidDescription", null!, 0, null!, 0, null!);
            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void Should_Have_Error_When_Name_Is_Empty()
        {
            var command = new UpdateLicenseCommand(Guid.NewGuid(), "", "ValidDescription", null!, 0, null!, 0, null!);
            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Should_Have_Error_When_Name_Exceeds_MaxLength()
        {
            var command = new UpdateLicenseCommand(Guid.NewGuid(), new string('a', 129), "ValidDescription", null!, 0, null!, 0, null!);
            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Should_Have_Error_When_Description_Is_Empty()
        {
            var command = new UpdateLicenseCommand(Guid.NewGuid(), "ValidName", "", null!, 0, null!, 0, null!);
            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void Should_Have_Error_When_Description_Exceeds_MaxLength()
        {
            var command = new UpdateLicenseCommand(Guid.NewGuid(), "ValidName", new string('a', 513), null!, 0, null!, 0, null!);
            var result = _validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Description);
        }

        [Fact]
        public void Should_Not_Have_Error_When_Command_Is_Valid()
        {
            var command = new UpdateLicenseCommand(Guid.NewGuid(), "ValidName", "ValidDescription", null!, 0, null!, 0, null!);
            var result = _validator.TestValidate(command);
            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}
