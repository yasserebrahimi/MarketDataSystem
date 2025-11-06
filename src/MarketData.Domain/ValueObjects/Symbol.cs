namespace MarketData.Domain.ValueObjects;

/// <summary>
/// Value object representing a trading symbol
/// </summary>
public record Symbol
{
    public string Value { get; init; }

    public Symbol(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Symbol cannot be empty", nameof(value));

        if (value.Length > 10)
            throw new ArgumentException("Symbol cannot exceed 10 characters", nameof(value));

        Value = value.ToUpperInvariant();
    }

    public static implicit operator string(Symbol symbol) => symbol.Value;
    public static implicit operator Symbol(string value) => new(value);

    public override string ToString() => Value;
}
