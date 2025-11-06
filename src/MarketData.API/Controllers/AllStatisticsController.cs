using System.Threading;
using System.Threading.Tasks;
using MediatR;
using MarketData.Application.Queries;
using Microsoft.AspNetCore.Mvc;

namespace MarketData.API.Controllers
{
    [ApiController]
    [Route("api/prices")]
    public sealed class AllStatisticsController : ControllerBase
    {
        private readonly IMediator _mediator;

        public AllStatisticsController(IMediator mediator)
        {
            _mediator = mediator;
        }

        [HttpGet]
        public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
        {
            var query = new GetAllStatisticsQuery();
            var result = await _mediator.Send(query, cancellationToken);
            return Ok(result);
        }
    }
}
