namespace MarketData.Application.DTOs;

/// <summary>
/// DTO for price update operation result
/// Used to transfer data between layers without exposing domain entities
/// </summary>
public class PriceUpdateResultDto
{
    public string Symbol { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public DateTime Timestamp { get; set; }
    public DateTime ProcessedAt { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
}
