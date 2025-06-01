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

    }
}
