using System;
using System.Threading;
using System.Threading.Tasks;
using MarketData.Application.Interfaces;
using MarketData.Domain.Entities;
using MarketData.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketData.Infrastructure.Processing
{
    public sealed class SimulatedMarketDataFeedHostedService : BackgroundService
    {
        private readonly ILogger<SimulatedMarketDataFeedHostedService> _logger;
        private readonly IMarketDataProcessor _processor;
        private readonly MarketDataProcessingOptions _options;
        private readonly Random _rng = new Random();

        public SimulatedMarketDataFeedHostedService(
            ILogger<SimulatedMarketDataFeedHostedService> logger,
            IMarketDataProcessor processor,
            IOptions<MarketDataProcessingOptions> options)
        {
            _logger = logger;
            _processor = processor;
            _options = options.Value;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            if (!_options.Simulation.Enabled)
            {
                _logger.LogInformation("Simulated feed disabled.");
                return;
            }

            var symbols = _options.Simulation.Symbols;
            _logger.LogInformation("Simulated feed starting for {Count} symbols at ~{Rate} tps.", symbols.Length, _options.Simulation.TicksPerSecond);

            var prices = new decimal[symbols.Length];
            for (int i = 0; i < prices.Length; i++)
                prices[i] = _options.Simulation.InitialPrice;

            double intervalMs = 1000.0 / Math.Max(1, _options.Simulation.TicksPerSecond);
            var delay = TimeSpan.FromMilliseconds(intervalMs);

            while (!stoppingToken.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow.UtcDateTime;
                for (int i = 0; i < symbols.Length; i++)
                {
                    decimal jitter = (decimal)(_rng.NextDouble() * 2 - 1) * _options.Simulation.MaxJitterPercent;
                    decimal newPrice = prices[i] * (1 + jitter);
                    if (newPrice <= 0) newPrice = prices[i];
                    prices[i] = newPrice;

                    await _processor.EnqueueUpdateAsync(PriceUpdate.Create(symbols[i], newPrice, now), stoppingToken);
                }

                try { await Task.Delay(delay, stoppingToken); }
                catch (OperationCanceledException) { break; }
            }
        }
    }
}
