using MarketData.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace MarketData.Infrastructure.Data;

/// <summary>
/// Main database context for MarketData system
/// </summary>
public class MarketDataDbContext : DbContext
{
    public MarketDataDbContext(DbContextOptions<MarketDataDbContext> options)
        : base(options)
    {
    }

    public DbSet<PriceUpdate> PriceUpdates => Set<PriceUpdate>();
    public DbSet<SymbolStatistics> SymbolStatistics => Set<SymbolStatistics>();
    public DbSet<PriceAnomaly> PriceAnomalies => Set<PriceAnomaly>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all configurations from the assembly
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return base.SaveChangesAsync(cancellationToken);
    }
}
