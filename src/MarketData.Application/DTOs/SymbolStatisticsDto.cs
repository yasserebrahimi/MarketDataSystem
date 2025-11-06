namespace MarketData.Application.DTOs;

/// <summary>
/// DTO for symbol statistics
/// </summary>
public class SymbolStatisticsDto
{
    public string Symbol { get; set; } = string.Empty;
    public decimal CurrentPrice { get; set; }
    public decimal MovingAverage { get; set; }
    public long UpdateCount { get; set; }
    public DateTime LastUpdateTime { get; set; }
    public decimal MinPrice { get; set; }
    public decimal MaxPrice { get; set; }
    
    /// <summary>
    /// Price change percentage from moving average
    /// </summary>
    public decimal ChangeFromAverage =>
        MovingAverage > 0
            ? ((CurrentPrice - MovingAverage) / MovingAverage) * 100
            : 0;
}
