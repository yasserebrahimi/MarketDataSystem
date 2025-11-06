namespace MarketData.Domain.Entities;

/// <summary>
/// Aggregated statistics for a symbol
/// </summary>
public class SymbolStatistics
{
    public string Symbol { get; private set; }
    public decimal CurrentPrice { get; private set; }
    public decimal MovingAverage { get; private set; }
    public long UpdateCount { get; private set; }
    public DateTime LastUpdateTime { get; private set; }
    public decimal MinPrice { get; private set; }
    public decimal MaxPrice { get; private set; }

    private SymbolStatistics() { }

    public SymbolStatistics(string symbol)
    {
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        MinPrice = decimal.MaxValue;
        MaxPrice = decimal.MinValue;
        UpdateCount = 0;
    }

    public void Update(decimal price, decimal movingAverage)
    {
        CurrentPrice = price;
        MovingAverage = movingAverage;
        UpdateCount++;
        LastUpdateTime = DateTime.UtcNow;
        
        if (price < MinPrice) MinPrice = price;
        if (price > MaxPrice) MaxPrice = price;
    }
}
