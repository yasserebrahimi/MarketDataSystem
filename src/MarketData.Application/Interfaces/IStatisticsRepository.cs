using MarketData.Domain.Entities;

namespace MarketData.Application.Interfaces;

/// <summary>
/// Repository for symbol statistics
/// Follows repository pattern for data access abstraction
/// </summary>
public interface IStatisticsRepository
{
    Task<SymbolStatistics?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default);
    Task<IEnumerable<SymbolStatistics>> GetAllAsync(CancellationToken cancellationToken = default);
    Task UpdateAsync(SymbolStatistics statistics, CancellationToken cancellationToken = default);
}
