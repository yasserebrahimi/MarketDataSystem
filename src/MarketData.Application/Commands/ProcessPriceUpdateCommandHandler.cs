using MediatR;
using Microsoft.Extensions.Logging;
using MarketData.Application.DTOs;
using MarketData.Application.Interfaces;
using MarketData.Domain.Entities;

namespace MarketData.Application.Commands;

/// <summary>
/// Handler for processing price update commands
/// Implements single responsibility - only handles price processing logic
/// </summary>
public class ProcessPriceUpdateCommandHandler : IRequestHandler<ProcessPriceUpdateCommand, PriceUpdateResultDto>
{
    private readonly IMarketDataProcessor _processor;
    private readonly ILogger<ProcessPriceUpdateCommandHandler> _logger;

    public ProcessPriceUpdateCommandHandler(
        IMarketDataProcessor processor,
        ILogger<ProcessPriceUpdateCommandHandler> logger)
    {
        _processor = processor ?? throw new ArgumentNullException(nameof(processor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<PriceUpdateResultDto> Handle(
        ProcessPriceUpdateCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing price update for {Symbol} at ${Price}",
            request.Symbol,
            request.Price);

        try
        {
            // Create domain entity
            var priceUpdate = new PriceUpdate(
                request.Symbol,
                request.Price,
                request.Timestamp);

            // Process through the market data processor
            await _processor.EnqueueUpdateAsync(priceUpdate, cancellationToken);

            _logger.LogDebug(
                "Successfully enqueued price update for {Symbol}",
                request.Symbol);

            return new PriceUpdateResultDto
            {
                Symbol = request.Symbol,
                Price = request.Price,
                Timestamp = request.Timestamp,
                ProcessedAt = DateTime.UtcNow,
                Success = true
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process price update for {Symbol}",
                request.Symbol);

            throw;
        }
    }
}
