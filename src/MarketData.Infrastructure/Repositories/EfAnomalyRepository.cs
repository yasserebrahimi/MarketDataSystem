using MarketData.Application.Interfaces;
using MarketData.Domain.Entities;
using MarketData.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MarketData.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of anomaly repository with database persistence
/// </summary>
public class EfAnomalyRepository : IAnomalyRepository
{
    private readonly MarketDataDbContext _context;
    private readonly ILogger<EfAnomalyRepository> _logger;

    public EfAnomalyRepository(
        MarketDataDbContext context,
        ILogger<EfAnomalyRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task AddAsync(PriceAnomaly anomaly, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.PriceAnomalies.Add(anomaly);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding anomaly for symbol {Symbol}", anomaly.Symbol);
            throw;
        }
    }

    public async Task<IReadOnlyCollection<PriceAnomaly>> GetRecentAsync(
        int take = 100,
        string? symbol = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.PriceAnomalies.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(symbol))
            {
                query = query.Where(a => a.Symbol == symbol);
            }

            return await query
                .OrderByDescending(a => a.DetectedAt)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving recent anomalies");
            throw;
        }
    }

    public async Task<long> CountAsync(string? symbol = null, CancellationToken cancellationToken = default)
    {
        try
        {
            var query = _context.PriceAnomalies.AsQueryable();

            if (!string.IsNullOrWhiteSpace(symbol))
            {
                query = query.Where(a => a.Symbol == symbol);
            }

            return await query.CountAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error counting anomalies");
            throw;
        }
    }
}
