using MarketData.Domain.Entities;

namespace MarketData.Application.Interfaces;

/// <summary>
/// Interface for the core market data processing engine
/// Abstracts the implementation details from the application layer
/// </summary>
public interface IMarketDataProcessor
{
    /// <summary>
    /// Enqueues a price update for processing
    /// </summary>
    Task EnqueueUpdateAsync(PriceUpdate update, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets current processing statistics
    /// </summary>
    Task<ProcessingStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the processing engine
    /// </summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the processing engine gracefully
    /// </summary>
    Task StopAsync(CancellationToken cancellationToken = default);
}

public class ProcessingStatistics
{
    public long TotalProcessed { get; set; }
    public long AnomaliesDetected { get; set; }
    public int ActiveSymbols { get; set; }
    public int QueueSize { get; set; }
    public double ThroughputPerSecond { get; set; }
}
