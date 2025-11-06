using MediatR;
using MarketData.Application.DTOs;

namespace MarketData.Application.Queries;

/// <summary>
/// Query to retrieve statistics for a specific symbol
/// Follows CQRS pattern - queries only read data
/// </summary>
public record GetSymbolStatisticsQuery : IRequest<SymbolStatisticsDto?>
{
    public string Symbol { get; init; } = string.Empty;
}
