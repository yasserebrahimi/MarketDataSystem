using MediatR;
using Microsoft.Extensions.Logging;
using MarketData.Application.DTOs;
using MarketData.Application.Interfaces;

namespace MarketData.Application.Queries;

/// <summary>
/// Handler for retrieving symbol statistics
/// </summary>
public class GetSymbolStatisticsQueryHandler : IRequestHandler<GetSymbolStatisticsQuery, SymbolStatisticsDto?>
{
    private readonly IStatisticsRepository _repository;
    private readonly ILogger<GetSymbolStatisticsQueryHandler> _logger;

    public GetSymbolStatisticsQueryHandler(
        IStatisticsRepository repository,
        ILogger<GetSymbolStatisticsQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SymbolStatisticsDto?> Handle(
        GetSymbolStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Retrieving statistics for symbol {Symbol}", request.Symbol);

        var statistics = await _repository.GetBySymbolAsync(request.Symbol, cancellationToken);

        if (statistics == null)
        {
            _logger.LogWarning("No statistics found for symbol {Symbol}", request.Symbol);
            return null;
        }

        return new SymbolStatisticsDto
        {
            Symbol = statistics.Symbol,
            CurrentPrice = statistics.CurrentPrice,
            MovingAverage = statistics.MovingAverage,
            UpdateCount = statistics.UpdateCount,
            LastUpdateTime = statistics.LastUpdateTime,
            MinPrice = statistics.MinPrice,
            MaxPrice = statistics.MaxPrice
        };
    }
}
