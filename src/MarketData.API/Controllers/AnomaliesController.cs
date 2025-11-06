using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MarketData.Application.Queries;
using Microsoft.AspNetCore.Mvc;

namespace MarketData.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public sealed class AnomaliesController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AnomaliesController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetAsync([FromQuery] string? symbol, [FromQuery] int take = 100, CancellationToken cancellationToken = default)
        {
            var query = new GetRecentAnomaliesQuery { Symbol = symbol, Take = take };
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }
    }
}
