using System;
using FluentValidation.TestHelper;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.AddModule;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License.Commands.AddModule
{
    public class AddModuleCommandTest
    {
        private readonly Validator validator;

        public AddModuleCommandTest()
        {
            validator = new Validator();
        }

        [Fact]
        public void Should_Have_Error_When_IdModule_Is_Empty()
        {
            var command = new AddModuleCommand(Guid.NewGuid(), Guid.Empty, "ValidName");
            var result = validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.IdModule);
        }

        [Fact]
        public void Should_Have_Error_When_Name_Is_Empty()
        {
            var command = new AddModuleCommand(Guid.NewGuid(), Guid.NewGuid(), string.Empty);
            var result = validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Should_Have_Error_When_Name_Exceeds_Maximum_Length()
        {
            var command = new AddModuleCommand(Guid.NewGuid(), Guid.NewGuid(), new string('a', 129));
            var result = validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Name);
        }

        [Fact]
        public void Should_Not_Have_Error_When_Command_Is_Valid()
        {
            var command = new AddModuleCommand(Guid.NewGuid(), Guid.NewGuid(), "ValidName");
            var result = validator.TestValidate(command);
            result.ShouldNotHaveAnyValidationErrors();
        }
    }
}
