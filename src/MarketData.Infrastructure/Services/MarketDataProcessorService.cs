using System.Collections.Concurrent;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using MarketData.Application.Interfaces;
using MarketData.Domain.Entities;

namespace MarketData.Infrastructure.Services;

/// <summary>
/// Core market data processing service
/// Implements high-performance concurrent processing with lock-free queues
/// </summary>
public class MarketDataProcessorService : IMarketDataProcessor
{
    private readonly ConcurrentQueue<PriceUpdate> _updateQueue;
    private readonly ConcurrentDictionary<string, SymbolDataProcessor> _symbolProcessors;
    private readonly ILogger<MarketDataProcessorService> _logger;
    private readonly CancellationTokenSource _cts;
    private readonly List<Task> _workerTasks;
    private readonly Stopwatch _stopwatch;

    private long _totalProcessed;
    private long _anomaliesDetected;
    private readonly int _workerCount;

    public MarketDataProcessorService(
        ILogger<MarketDataProcessorService> logger,
        int workerCount = 4)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _workerCount = workerCount;

        _updateQueue = new ConcurrentQueue<PriceUpdate>();
        _symbolProcessors = new ConcurrentDictionary<string, SymbolDataProcessor>();
        _cts = new CancellationTokenSource();
        _workerTasks = new List<Task>();
        _stopwatch = new Stopwatch();
    }

    public Task EnqueueUpdateAsync(PriceUpdate update, CancellationToken cancellationToken = default)
    {
        if (update == null) throw new ArgumentNullException(nameof(update));

        _updateQueue.Enqueue(update);

        _logger.LogDebug(
            "Enqueued update for {Symbol} at ${Price}",
            update.Symbol,
            update.Price);

        return Task.CompletedTask;
    }

    public Task<ProcessingStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
    {
        var elapsed = _stopwatch.Elapsed.TotalSeconds;
        var throughput = elapsed > 0 ? _totalProcessed / elapsed : 0;

        var stats = new ProcessingStatistics
        {
            TotalProcessed = _totalProcessed,
            AnomaliesDetected = _anomaliesDetected,
            ActiveSymbols = _symbolProcessors.Count,
            QueueSize = _updateQueue.Count,
            ThroughputPerSecond = throughput
        };

        return Task.FromResult(stats);
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Starting market data processor with {WorkerCount} workers",
            _workerCount);

        _stopwatch.Start();

        // Start worker tasks
        for (int i = 0; i < _workerCount; i++)
        {
            var workerId = i;
            _workerTasks.Add(Task.Run(
                () => ProcessWorkerAsync(workerId, _cts.Token),
                _cts.Token));
        }

        _logger.LogInformation("Market data processor started successfully");

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Stopping market data processor");

        _cts.Cancel();
        _stopwatch.Stop();

        try
        {
            await Task.WhenAll(_workerTasks);
        }
        catch (OperationCanceledException)
        {
            // Expected during shutdown
        }

        _logger.LogInformation(
            "Market data processor stopped. Processed {Total} updates",
            _totalProcessed);
    }

    private async Task ProcessWorkerAsync(int workerId, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Worker {WorkerId} started", workerId);

        while (!cancellationToken.IsCancellationRequested)
        {
            if (_updateQueue.TryDequeue(out var update))
            {
                try
                {
                    ProcessUpdate(update);
                    Interlocked.Increment(ref _totalProcessed);
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Worker {WorkerId} failed to process update for {Symbol}",
                        workerId,
                        update.Symbol);
                }
            }
            else
            {
                // Yield to prevent CPU spinning
                await Task.Delay(1, cancellationToken);
            }
        }

        _logger.LogDebug("Worker {WorkerId} stopped", workerId);
    }

    private void ProcessUpdate(PriceUpdate update)
    {
        // Get or create symbol processor
        var processor = _symbolProcessors.GetOrAdd(
            update.Symbol,
            key => new SymbolDataProcessor(key, 100));

        // Process the update
        var result = processor.Process(update);

        if (result.IsAnomaly)
        {
            Interlocked.Increment(ref _anomaliesDetected);

            _logger.LogWarning(
                "Anomaly detected for {Symbol}: ${OldPrice} -> ${NewPrice} ({Change:F2}%)",
                update.Symbol,
                result.OldPrice,
                result.NewPrice,
                result.ChangePercent);
        }
    }
}

/// <summary>
/// Processes updates for a single symbol
/// Thread-safe with fine-grained locking
/// </summary>
internal class SymbolDataProcessor
{
    private readonly string _symbol;
    private readonly MovingAverageCalculator _movingAverage;
    private readonly object _lock = new object();
    private decimal _lastPrice;
    private DateTime _lastUpdateTime;

    public SymbolDataProcessor(string symbol, int windowSize)
    {
        _symbol = symbol;
        _movingAverage = new MovingAverageCalculator(windowSize);
    }

    public ProcessingResult Process(PriceUpdate update)
    {
        lock (_lock)
        {
            var oldPrice = _lastPrice;
            var newPrice = update.Price;

            // Update moving average
            var movingAvg = _movingAverage.AddPrice(newPrice);

            // Detect anomaly
            var isAnomaly = false;
            var changePercent = 0m;

            if (_lastPrice > 0 && _lastUpdateTime > DateTime.MinValue)
            {
                var timeDiff = update.Timestamp - _lastUpdateTime;
                if (timeDiff <= TimeSpan.FromSeconds(1))
                {
                    changePercent = Math.Abs((newPrice - oldPrice) / oldPrice) * 100;
                    isAnomaly = changePercent > 2.0m;
                }
            }

            _lastPrice = newPrice;
            _lastUpdateTime = update.Timestamp;

            return new ProcessingResult
            {
                Symbol = _symbol,
                OldPrice = oldPrice,
                NewPrice = newPrice,
                MovingAverage = movingAvg,
                ChangePercent = changePercent,
                IsAnomaly = isAnomaly
            };
        }
    }
}

internal class ProcessingResult
{
    public string Symbol { get; set; } = string.Empty;
    public decimal OldPrice { get; set; }
    public decimal NewPrice { get; set; }
    public decimal MovingAverage { get; set; }
    public decimal ChangePercent { get; set; }
    public bool IsAnomaly { get; set; }
}

/// <summary>
/// O(1) Moving Average Calculator
/// Uses sliding window with running sum
/// </summary>
internal class MovingAverageCalculator
{
    private readonly Queue<decimal> _window;
    private readonly int _windowSize;
    private decimal _sum;
    private readonly object _lock = new object();

    public MovingAverageCalculator(int windowSize)
    {
        _windowSize = windowSize;
        _window = new Queue<decimal>(windowSize);
        _sum = 0;
    }

    public decimal AddPrice(decimal price)
    {
        lock (_lock)
        {
            _window.Enqueue(price);
            _sum += price;

            if (_window.Count > _windowSize)
            {
                _sum -= _window.Dequeue();
            }

            return _window.Count > 0 ? _sum / _window.Count : 0;
        }
    }

    public decimal GetAverage()
    {
        lock (_lock)
        {
            return _window.Count > 0 ? _sum / _window.Count : 0;
        }
    }
}