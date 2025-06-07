using System.Text.Json.Serialization;

namespace CodeDesignPlus.Net.Microservice.Licenses.Domain.ValueObjects;

public sealed partial class Currency
{
    public Guid Id { get; private set; }
    public string Name { get; private set; }
    public string Code { get; private set; }
    public string Symbol { get; private set; }

    [JsonConstructor]
    public Currency(Guid id, string name, string code, string symbol)
    {
        DomainGuard.GuidIsEmpty(id, Errors.CurrencyIdIsEmpty);
        
        DomainGuard.IsNullOrEmpty(name, Errors.NameCurrencyIsRequired);
        DomainGuard.IsNullOrEmpty(code, Errors.CodeCurrencyIsRequired);
        DomainGuard.IsNullOrEmpty(symbol, Errors.SymbolCurrencyIsRequired);

        this.Id = id;
        this.Name = name;
        this.Code = code;
        this.Symbol = symbol;
    }

    public static Currency Create(Guid id, string name, string code, string symbol)
    {
        return new Currency(id, name, code, symbol);
    }
}