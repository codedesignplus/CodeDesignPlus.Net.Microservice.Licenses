using Newtonsoft.Json;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Currency
{
    public string Name { get; private set; } = null!;
    public string Code { get; private set; } = null!;
    public string Symbol { get; private set; } = null!;

    [JsonConstructor]
    private Currency(string name, string code, string symbol)
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
