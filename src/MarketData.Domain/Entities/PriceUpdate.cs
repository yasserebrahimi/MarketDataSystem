namespace MarketData.Domain.Entities;

/// <summary>
/// Represents a single price update for a financial instrument
/// </summary>
public class PriceUpdate
{
    public Guid Id { get; private set; }
    public string Symbol { get; private set; }
    public decimal Price { get; private set; }
    public DateTime Timestamp { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Private constructor for EF Core
    private PriceUpdate() { }

    /// <summary>
    /// Creates a new price update
    /// </summary>
    public PriceUpdate(string symbol, decimal price, DateTime timestamp)
    {
        if (string.IsNullOrWhiteSpace(symbol))
            throw new ArgumentException("Symbol cannot be empty", nameof(symbol));
        
        if (price <= 0)
            throw new ArgumentException("Price must be positive", nameof(price));

        Id = Guid.NewGuid();
        Symbol = symbol.ToUpperInvariant();
        Price = price;
        Timestamp = timestamp;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdatePrice(decimal newPrice)
    {
        if (newPrice <= 0)
            throw new ArgumentException("Price must be positive", nameof(newPrice));
        
        Price = newPrice;
        Timestamp = DateTime.UtcNow;
    }
}
