using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarketData.Application.Interfaces;
using MarketData.Domain.Entities;
using MarketData.Infrastructure.Processing;

namespace MarketData.Infrastructure.Repositories
{
    /// <summary>
    /// Adapts the processor state to the repository abstraction.
    /// </summary>
    public sealed class InMemoryStatisticsRepository : IStatisticsRepository
    {
        private readonly HighPerformanceMarketDataProcessorService _processor;

        public InMemoryStatisticsRepository(HighPerformanceMarketDataProcessorService processor)
        {
            _processor = processor;
        }

        public Task<SymbolStatistics?> GetBySymbolAsync(string symbol, CancellationToken cancellationToken = default)
        {
            var s = _processor.TryGetSymbolStatistics(symbol);
            return Task.FromResult(s);
        }

        public Task<IEnumerable<SymbolStatistics>> GetAllAsync(CancellationToken cancellationToken = default)
        {
            var list = _processor.GetAllStatistics().ToList();
            return Task.FromResult<IEnumerable<SymbolStatistics>>(list);
        }

        public Task UpdateAsync(SymbolStatistics statistics, CancellationToken cancellationToken = default)
        {
            // No-op in this in-memory model; state is updated by the processor.
            return Task.CompletedTask;
        }
    }
}
