using MarketData.Domain.Entities;
using MarketData.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace MarketData.Infrastructure.Repositories;

/// <summary>
/// Repository for managing price updates with database persistence
/// </summary>
public class PriceUpdateRepository
{
    private readonly MarketDataDbContext _context;
    private readonly ILogger<PriceUpdateRepository> _logger;

    public PriceUpdateRepository(
        MarketDataDbContext context,
        ILogger<PriceUpdateRepository> logger)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task AddAsync(PriceUpdate priceUpdate, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.PriceUpdates.Add(priceUpdate);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding price update for symbol {Symbol}", priceUpdate.Symbol);
            throw;
        }
    }

    public async Task AddBulkAsync(IEnumerable<PriceUpdate> priceUpdates, CancellationToken cancellationToken = default)
    {
        try
        {
            _context.PriceUpdates.AddRange(priceUpdates);
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding bulk price updates");
            throw;
        }
    }

    public async Task<IReadOnlyCollection<PriceUpdate>> GetBySymbolAsync(
        string symbol,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.PriceUpdates
                .AsNoTracking()
                .Where(p => p.Symbol == symbol)
                .OrderByDescending(p => p.Timestamp)
                .Take(take)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving price updates for symbol {Symbol}", symbol);
            throw;
        }
    }

    public async Task<PriceUpdate?> GetLatestBySymbolAsync(string symbol, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _context.PriceUpdates
                .AsNoTracking()
                .Where(p => p.Symbol == symbol)
                .OrderByDescending(p => p.Timestamp)
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving latest price update for symbol {Symbol}", symbol);
            throw;
        }
    }
}
