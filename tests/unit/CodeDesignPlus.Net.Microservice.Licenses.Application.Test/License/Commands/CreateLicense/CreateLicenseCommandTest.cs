using System;
using CodeDesignPlus.Net.Microservice.Licenses.Application.License.Commands.CreateLicense;
using FluentValidation.TestHelper;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Application.Test.License.Commands.CreateLicense
{
    public class CreateLicenseCommandTest
    {
        private readonly Validator _validator;

        public CreateLicenseCommandTest()
        {
            _validator = new Validator();
        }

    }
}
