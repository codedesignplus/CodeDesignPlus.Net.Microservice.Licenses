namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

/// <summary>
/// Qué tipos de documento se escriben con guion y dígito de verificación, y con qué pesos se calcula ese dígito.
/// </summary>
/// <remarks>
/// <para>
/// Las dos listas son configuración del despliegue (<c>TenantDocument</c>), no de este micro, que es transversal y no lleva
/// un país dentro: el tipo es un código del catálogo de tipos de documento y los pesos los fija la autoridad tributaria de
/// cada país. Aquí solo vive el cálculo genérico, un módulo 11 ponderado: cada dígito del número, de derecha a izquierda,
/// se multiplica por su peso; la suma se divide entre 11, y si el residuo es 0 o 1 ese es el dígito, si no, 11 menos el
/// residuo.
/// </para>
/// <para>
/// El número y su dígito van en el mismo campo, «número-dígito». Sin tipos configurados, ningún documento lo exige.
/// </para>
/// </remarks>
/// <param name="DocumentTypes">Los códigos de los tipos de documento que llevan dígito de verificación.</param>
/// <param name="Weights">Los pesos, del dígito de las unidades hacia la izquierda.</param>
public sealed record DocumentCheckDigitRules(IReadOnlyCollection<string> DocumentTypes, IReadOnlyList<int> Weights)
{
    /// <summary>
    /// El divisor del cálculo. Es la definición del módulo 11, no un dato del país.
    /// </summary>
    private const int Modulus = 11;

    /// <summary>
    /// Las reglas de un despliegue que no configura ningún tipo con dígito de verificación.
    /// </summary>
    public static readonly DocumentCheckDigitRules None = new([], []);

    /// <summary>
    /// Si un tipo de documento lleva dígito de verificación.
    /// </summary>
    /// <param name="documentTypeCode">El código del tipo.</param>
    /// <returns><c>true</c> si está entre los configurados.</returns>
    public bool Applies(string? documentTypeCode)
    {
        return !string.IsNullOrWhiteSpace(documentTypeCode) && this.DocumentTypes.Any(code => string.Equals(code.Trim(), documentTypeCode.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Calcula el dígito de verificación de un número.
    /// </summary>
    /// <param name="number">El número, solo dígitos y sin el dígito de verificación.</param>
    /// <returns>El dígito, o <c>null</c> si el número está vacío, no es solo dígitos o es más largo que la lista de pesos.</returns>
    public int? Compute(string? number)
    {
        if (string.IsNullOrEmpty(number) || number.Length > this.Weights.Count || !number.All(char.IsAsciiDigit))
        {
            return null;
        }

        var sum = 0;

        for (var i = 0; i < number.Length; i++)
        {
            sum += (number[number.Length - 1 - i] - '0') * this.Weights[i];
        }

        var remainder = sum % Modulus;

        return remainder > 1 ? Modulus - remainder : remainder;
    }

    /// <summary>
    /// Rechaza el número de un documento que lleva dígito de verificación si no viene con guion y con el dígito que le corresponde.
    /// </summary>
    /// <remarks>
    /// El error dice qué corregir y cuál es el dígito. A un número sin guion no se le adivina si el último dígito es el de
    /// verificación pegado, porque el número no tiene una longitud fija. Si el último dígito es justo el de verificación de
    /// los anteriores, puede traerlo pegado o no, y el error da las dos escrituras, primero la de separarlo. Si no lo es,
    /// el error dice cómo se escribe si lo recibido es el número sin el dígito.
    /// </remarks>
    /// <param name="documentTypeCode">El código del tipo de documento.</param>
    /// <param name="documentNumber">El número como se escribió.</param>
    /// <exception cref="CodeDesignPlus.Net.Exceptions.CodeDesignPlusException">Si falta el guion y el dígito, si el número no es solo dígitos o si el dígito no corresponde.</exception>
    public void Ensure(string? documentTypeCode, string? documentNumber)
    {
        if (!this.Applies(documentTypeCode))
        {
            return;
        }

        var value = documentNumber?.Trim() ?? string.Empty;
        var dash = value.LastIndexOf('-');
        var isMissing = dash < 0;

        var number = isMissing ? value : value[..dash];
        var checkDigit = this.Compute(number);

        var isWellFormed = checkDigit is not null && (isMissing || (dash == value.Length - 2 && char.IsAsciiDigit(value[^1])));

        DomainGuard.IsFalse(isWellFormed, Errors.DocumentNumberFormatIsInvalid);

        var mayBeAttached = isMissing && this.Compute(number[..^1]) == number[^1] - '0';

        DomainGuard.IsTrue(mayBeAttached, Errors.DocumentCheckDigitMayBeAttached.With(number, number[..^1], number[^1], checkDigit));
        DomainGuard.IsTrue(isMissing, Errors.DocumentCheckDigitIsMissing.With(number, checkDigit));
        DomainGuard.IsTrue(value[^1] - '0' != checkDigit, Errors.DocumentCheckDigitDoesNotMatch.With(number, checkDigit, value[^1]));
    }
}
