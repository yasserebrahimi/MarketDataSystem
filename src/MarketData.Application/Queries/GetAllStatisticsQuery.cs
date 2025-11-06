using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MarketData.Application.DTOs;
using MarketData.Application.Interfaces;

namespace MarketData.Application.Queries
{
    public sealed class GetAllStatisticsQuery : IRequest<IReadOnlyCollection<SymbolStatisticsDto>>
    {
    }

    public sealed class GetAllStatisticsQueryHandler : IRequestHandler<GetAllStatisticsQuery, IReadOnlyCollection<SymbolStatisticsDto>>
    {
        private readonly IStatisticsRepository _repo;

        public GetAllStatisticsQueryHandler(IStatisticsRepository repo)
        {
            _repo = repo;
        }

        public async Task<IReadOnlyCollection<SymbolStatisticsDto>> Handle(GetAllStatisticsQuery request, CancellationToken cancellationToken)
        {
            var stats = await _repo.GetAllAsync(cancellationToken);
            return stats.Select(s => new SymbolStatisticsDto
            {
                Symbol = s.Symbol,
                CurrentPrice = s.CurrentPrice,
                MovingAverage = s.MovingAverage,
                UpdateCount = s.UpdateCount,
                LastUpdateTime = s.LastUpdateTime,
                MinPrice = s.MinPrice,
                MaxPrice = s.MaxPrice
            }).ToList();
        }
    }
}
