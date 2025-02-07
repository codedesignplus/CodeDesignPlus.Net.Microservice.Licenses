using System;
using Xunit;
using FluentValidation.TestHelper;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.RemoveModule;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License.Commands.RemoveModule
{
    public class RemoveModuleCommandTest
    {
        private readonly Validator validator;

        public RemoveModuleCommandTest()
        {
            validator = new Validator();
        }

        [Fact]
        public void Should_Have_Error_When_Id_Is_Empty()
        {
            var command = new RemoveModuleCommand(Guid.Empty, Guid.NewGuid());
            var result = validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void Should_Have_Error_When_IdModule_Is_Empty()
        {
            var command = new RemoveModuleCommand(Guid.NewGuid(), Guid.Empty);
            var result = validator.TestValidate(command);
            result.ShouldHaveValidationErrorFor(x => x.IdModule);
        }

        [Fact]
        public void Should_Not_Have_Error_When_Ids_Are_Valid()
        {
            var command = new RemoveModuleCommand(Guid.NewGuid(), Guid.NewGuid());
            var result = validator.TestValidate(command);
            result.ShouldNotHaveValidationErrorFor(x => x.Id);
            result.ShouldNotHaveValidationErrorFor(x => x.IdModule);
        }
    }
}
