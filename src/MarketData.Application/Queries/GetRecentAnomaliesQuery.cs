using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MarketData.Application.DTOs;
using MarketData.Application.Interfaces;

namespace MarketData.Application.Queries
{
    public sealed class GetRecentAnomaliesQuery : IRequest<IReadOnlyCollection<PriceAnomalyDto>>
    {
        public string? Symbol { get; init; }
        public int Take { get; init; } = 100;
    }

    public sealed class GetRecentAnomaliesQueryHandler : IRequestHandler<GetRecentAnomaliesQuery, IReadOnlyCollection<PriceAnomalyDto>>
    {
        private readonly IAnomalyRepository _repo;

        public GetRecentAnomaliesQueryHandler(IAnomalyRepository repo)
        {
            _repo = repo;
        }

        public async Task<IReadOnlyCollection<PriceAnomalyDto>> Handle(GetRecentAnomaliesQuery request, CancellationToken cancellationToken)
        {
            var anomalies = await _repo.GetRecentAsync(request.Take, request.Symbol, cancellationToken);
            return anomalies.Select(a => new PriceAnomalyDto
            {
                Symbol = a.Symbol,
                OldPrice = a.OldPrice,
                NewPrice = a.NewPrice,
                ChangePercent = a.ChangePercent,
                Severity = a.Severity,
                DetectedAt = a.DetectedAt
            }).ToList();
        }
    }
}
