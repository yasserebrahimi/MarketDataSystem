using MediatR;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using MarketData.Application.Commands;
using MarketData.Application.Queries;

namespace MarketData.API.Controllers;

/// <summary>
/// Controller for price operations
/// Follows REST principles and proper HTTP semantics
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[Authorize(Policy = "RequireReadAccess")]
public class PricesController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly ILogger<PricesController> _logger;

    public PricesController(IMediator mediator, ILogger<PricesController> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Process a new price update
    /// </summary>
    /// <param name="command">Price update details</param>
    /// <returns>Processing result</returns>
    /// <response code="200">Price update processed successfully</response>
    /// <response code="400">Invalid request</response>
    /// <response code="401">Unauthorized - Write access required</response>
    [HttpPost]
    [Authorize(Policy = "RequireWriteAccess")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> ProcessUpdate([FromBody] ProcessPriceUpdateCommand command)
    {
        _logger.LogInformation(
            "Received price update request for {Symbol}",
            command.Symbol);

        var result = await _mediator.Send(command);
        return Ok(result);
    }

    /// <summary>
    /// Get statistics for a specific symbol
    /// </summary>
    /// <param name="symbol">Trading symbol</param>
    /// <returns>Symbol statistics</returns>
    [HttpGet("{symbol}/statistics")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetStatistics(string symbol)
    {
        var query = new GetSymbolStatisticsQuery { Symbol = symbol };
        var result = await _mediator.Send(query);

        if (result == null)
        {
            return NotFound(new { message = $"No statistics found for symbol {symbol}" });
        }

        return Ok(result);
    }
}
