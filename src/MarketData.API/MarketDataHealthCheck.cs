using Microsoft.Extensions.Diagnostics.HealthChecks;
using MarketData.Application.Interfaces;

namespace MarketData.API;

/// <summary>
/// Health check for market data processor
/// </summary>
public class MarketDataHealthCheck : IHealthCheck
{
    private readonly IMarketDataProcessor _processor;

    public MarketDataHealthCheck(IMarketDataProcessor processor)
    {
        _processor = processor;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var stats = await _processor.GetStatisticsAsync(cancellationToken);
            
            var data = new Dictionary<string, object>
            {
                ["total_processed"] = stats.TotalProcessed,
                ["anomalies_detected"] = stats.AnomaliesDetected,
                ["active_symbols"] = stats.ActiveSymbols,
                ["queue_size"] = stats.QueueSize,
                ["throughput"] = $"{stats.ThroughputPerSecond:F0}/sec"
            };

            if (stats.QueueSize > 10000)
            {
                return HealthCheckResult.Degraded(
                    "Queue size is high",
                    data: data);
            }

            return HealthCheckResult.Healthy(
                "Market data processor is healthy",
                data: data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "Market data processor is unhealthy",
                ex);
        }
    }
}
