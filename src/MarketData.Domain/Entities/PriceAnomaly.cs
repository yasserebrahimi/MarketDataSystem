namespace MarketData.Domain.Entities;

/// <summary>
/// Represents a detected price anomaly
/// </summary>
public class PriceAnomaly
{
    public Guid Id { get; private set; }
    public string Symbol { get; private set; }
    public decimal OldPrice { get; private set; }
    public decimal NewPrice { get; private set; }
    public decimal ChangePercent { get; private set; }
    public DateTime DetectedAt { get; private set; }
    public string Severity { get; private set; }

    private PriceAnomaly() { }

    public PriceAnomaly(
        string symbol,
        decimal oldPrice,
        decimal newPrice,
        decimal changePercent)
    {
        Id = Guid.NewGuid();
        Symbol = symbol ?? throw new ArgumentNullException(nameof(symbol));
        OldPrice = oldPrice;
        NewPrice = newPrice;
        ChangePercent = changePercent;
        DetectedAt = DateTime.UtcNow;
        Severity = DetermineSeverity(changePercent);
    }

    private string DetermineSeverity(decimal changePercent)
    {
        var absChange = Math.Abs(changePercent);
        if (absChange > 5) return "Critical";
        if (absChange > 3) return "High";
        return "Medium";
    }
}
