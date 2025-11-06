using MarketData.Application.Interfaces;
using MarketData.Domain.Entities;
using MarketData.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MarketData.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of statistics repository with database persistence
/// </summary>
public class EfStatisticsRepository : IStatisticsRepository
{
    private readonly MarketDataDbContext _context;
    private readonly ILogger<EfStatisticsRepository> _logger;

    public EfStatisticsRepository(
        MarketDataDbContext context,
        ILogger<EfStatisticsRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<SymbolStatistics?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SymbolStatistics
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Symbol == symbol, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving statistics for symbol {Symbol}", symbol);
            throw;
        }
    }

    public async Task<IEnumerable<SymbolStatistics>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.SymbolStatistics
                .AsNoTracking()
                .OrderBy(s => s.Symbol)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all statistics");
            throw;
        }
    }

    public async Task UpdateAsync(SymbolStatistics statistics, CancellationToken cancellationToken = default)
    {
        try
        {
            var existing = await _context.SymbolStatistics
                .FindAsync(new object[] { statistics.Symbol }, cancellationToken);

            if (existing == null)
            {
                _context.SymbolStatistics.Add(statistics);
            }
            else
            {
                _context.Entry(existing).CurrentValues.SetValues(statistics);
            }

            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating statistics for symbol {Symbol}", statistics.Symbol);
            throw;
        }
    }
}
