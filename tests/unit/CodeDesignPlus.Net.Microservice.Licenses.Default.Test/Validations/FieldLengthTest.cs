using CodeDesignPlus.Net.xUnit.Microservice.Validations;
using Xunit;

namespace CodeDesignPlus.Net.Microservice.Licenses.Default.Test.Validations;

/// <summary>
/// Los campos de texto libre usan las longitudes estandar de FieldLength (regla 48 de rules/, plan 094).
/// </summary>
public class FieldLengthTest
{
    [Fact]
    public void Validators_FreeTextFields_UseStandardLengths()
    {
        var violations = StandardFieldLengths.FindViolations(
            typeof(Application.Errors).Assembly);

        Assert.Empty(violations);
    }
}
