using System.Text.Json.Serialization;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Currency
{
    [GeneratedRegex(@"^0x[0-9]{32}$")]
    private static partial Regex Regex();

    public string Name { get; private set; }
    public string Code { get; private set; }
    public string Symbol { get; private set; }

    [JsonConstructor]
    public Currency(string name, string code, string symbol)
    {
        DomainGuard.IsNullOrEmpty(name, Errors.NameCurrencyIsRequired);
        DomainGuard.IsNullOrEmpty(code, Errors.CodeCurrencyIsRequired);
        DomainGuard.IsNullOrEmpty(symbol, Errors.SymbolCurrencyIsRequired);

        this.Name = name;
        this.Code = code;
        this.Symbol = symbol;
    }

    public static Currency Create(string name, string code, string symbol)
    {
        return new Currency(name, code, symbol);
    }
}
