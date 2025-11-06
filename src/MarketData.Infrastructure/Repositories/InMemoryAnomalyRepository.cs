using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MarketData.Application.Interfaces;
using MarketData.Domain.Entities;
using MarketData.Infrastructure.Options;

namespace MarketData.Infrastructure.Repositories
{
    public sealed class InMemoryAnomalyRepository : IAnomalyRepository
    {
        private readonly ConcurrentQueue<PriceAnomaly> _queue = new();
        private readonly int _capacity;

        public InMemoryAnomalyRepository(MarketDataProcessingOptions options)
        {
            _capacity = Math.Max(100, options.RecentAnomaliesCapacity);
        }

        public Task AddAsync(PriceAnomaly anomaly, CancellationToken cancellationToken = default)
        {
            _queue.Enqueue(anomaly);
            while (_queue.Count > _capacity && _queue.TryDequeue(out _)) { }
            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<PriceAnomaly>> GetRecentAsync(int take = 100, string? symbol = null, CancellationToken cancellationToken = default)
        {
            var items = _queue.Reverse();
            if (!string.IsNullOrWhiteSpace(symbol))
                items = items.Where(a => string.Equals(a.Symbol, symbol, StringComparison.Ordinal));

            var list = items.Take(Math.Max(1, take)).ToList();
            return Task.FromResult((IReadOnlyCollection<PriceAnomaly>)list);
        }

        public Task<long> CountAsync(string? symbol = null, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(symbol)) return Task.FromResult((long)_queue.Count);
            long cnt = _queue.Count(a => string.Equals(a.Symbol, symbol, StringComparison.Ordinal));
            return Task.FromResult(cnt);
        }
    }
}
