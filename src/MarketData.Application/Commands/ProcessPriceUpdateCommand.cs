using MediatR;
using MarketData.Application.DTOs;

namespace MarketData.Application.Commands;

/// <summary>
/// Command to process a new price update
/// Follows CQRS pattern - commands represent state changes
/// </summary>
public record ProcessPriceUpdateCommand : IRequest<PriceUpdateResultDto>
{
    public string Symbol { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public DateTime Timestamp { get; init; }
}
