using System.Threading;
using System.Threading.Tasks;
using MarketData.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace MarketData.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class MetricsController : ControllerBase
    {
        private readonly IMarketDataProcessor _processor;

        public MetricsController(IMarketDataProcessor processor)
        {
            _processor = processor;
        }

        [HttpGet]
        public async Task<IActionResult> GetAsync(CancellationToken cancellationToken)
        {
            var stats = await _processor.GetStatisticsAsync(cancellationToken);
            return Ok(stats);
        }
    }
}
