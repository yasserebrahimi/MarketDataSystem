using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MarketData.Domain.Entities;

namespace MarketData.Application.Interfaces
{
    public interface IAnomalyRepository
    {
        Task AddAsync(PriceAnomaly anomaly, CancellationToken cancellationToken = default);
        Task<IReadOnlyCollection<PriceAnomaly>> GetRecentAsync(int take = 100, string? symbol = null, CancellationToken cancellationToken = default);
        Task<long> CountAsync(string? symbol = null, CancellationToken cancellationToken = default);
    }
}
