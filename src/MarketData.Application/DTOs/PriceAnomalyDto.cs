namespace MarketData.Application.DTOs
{
    public sealed class PriceAnomalyDto
    {
        public string Symbol { get; init; } = string.Empty;
        public decimal OldPrice { get; init; }
        public decimal NewPrice { get; init; }
        public decimal ChangePercent { get; init; }
        public string Severity { get; init; } = string.Empty;
        public DateTime DetectedAt { get; init; }
    }
}
