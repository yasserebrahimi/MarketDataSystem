using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using MarketData.Application.Interfaces;
using MarketData.Domain.Entities;
using MarketData.Infrastructure.Analytics;
using MarketData.Infrastructure.Options;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MarketData.Infrastructure.Processing
{
    /// <summary>
    /// Partitioned, channel-based high-performance processor.
    /// </summary>
    public sealed class HighPerformanceMarketDataProcessorService : IMarketDataProcessor, IHostedService
    {
        private readonly ILogger<HighPerformanceMarketDataProcessorService> _logger;
        private readonly MarketDataProcessingOptions _options;
        private readonly Channel<PriceUpdate>[] _channels;
        private readonly Task[] _workers;
        private readonly PartitionState[] _states;
        private readonly CancellationTokenSource _cts = new();
        private long _totalProcessed;
        private long _anomaliesDetected;

        private readonly IAnomalyRepository _anomalyRepo;

        public HighPerformanceMarketDataProcessorService(
            ILogger<HighPerformanceMarketDataProcessorService> logger,
            IOptions<MarketDataProcessingOptions> options,
            IAnomalyRepository anomalyRepo)
        {
            _logger = logger;
            _options = options.Value;
            _anomalyRepo = anomalyRepo;

            int partitions = _options.Partitions > 0 ? _options.Partitions : Environment.ProcessorCount;
            _channels = new Channel<PriceUpdate>[partitions];
            _workers = new Task[partitions];
            _states = new PartitionState[partitions];

            var chOpts = new BoundedChannelOptions(_options.ChannelCapacity)
            {
                SingleReader = true,
                SingleWriter = false,
                FullMode = BoundedChannelFullMode.DropOldest
            };

            for (int i = 0; i < partitions; i++)
            {
                _channels[i] = Channel.CreateBounded<PriceUpdate>(chOpts);
                _states[i] = new PartitionState(_options);
            }
        }

        public Task EnqueueUpdateAsync(PriceUpdate update, CancellationToken cancellationToken = default)
        {
            int pid = PartitionId(update.Symbol);
            _channels[pid].Writer.TryWrite(update);
            return Task.CompletedTask;
        }

        public async Task<ProcessingStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default)
        {
            var activeSymbols = _states.Sum(s => s.Symbols.Count);
            var queueSize = _channels.Sum(c => c.Reader.Count);
            var stats = new ProcessingStatistics
            {
                TotalProcessed = Interlocked.Read(ref _totalProcessed),
                AnomaliesDetected = Interlocked.Read(ref _anomaliesDetected),
                ActiveSymbols = activeSymbols,
                QueueSize = queueSize,
                ThroughputPerSecond = 0 // filled via external metrics if needed
            };
            await Task.CompletedTask;
            return stats;
        }

        public Task StartAsync(CancellationToken cancellationToken = default)
        {
            _logger.LogInformation("Starting {Workers} workers...", _workers.Length);
            for (int i = 0; i < _workers.Length; i++)
            {
                int pid = i;
                _workers[i] = Task.Run(() => WorkerLoopAsync(pid, _cts.Token));
            }
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken = default)
        {
            _cts.Cancel();
            return Task.WhenAll(_workers);
        }

        private async Task WorkerLoopAsync(int partitionId, CancellationToken ct)
        {
            var reader = _channels[partitionId].Reader;
            var state = _states[partitionId];

            try
            {
                while (await reader.WaitToReadAsync(ct))
                {
                    while (reader.TryRead(out var update))
                    {
                        ProcessUpdate(state, update);
                        Interlocked.Increment(ref _totalProcessed);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // normal shutdown
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker {Partition} crashed.", partitionId);
            }
        }

        private void ProcessUpdate(PartitionState partition, PriceUpdate update)
        {
            var symbol = update.Symbol;
            var s = partition.Symbols.GetOrAdd(symbol, _ => new SymbolState(symbol, _options));

            // Moving average
            double avg = s.MovingAverage.Add((double)update.Price);

            // Sliding window
            long tsMs = update.Timestamp.ToUniversalTime().ToUnixTimeMilliseconds();
            s.Window.AddSample(tsMs, (double)update.Price);

            // Anomaly detection
            if (s.Window.TryGetMinMax(tsMs, out var min, out var max))
            {
                decimal threshold = _options.AnomalyThresholdPercent / 100m;
                if (min > 0)
                {
                    decimal up = ((decimal)update.Price - (decimal)min) / (decimal)min;
                    if (up > threshold)
                    {
                        Interlocked.Increment(ref _anomaliesDetected);
                        var anomaly = PriceAnomaly.Create(symbol, (decimal)min, update.Price, up * 100m);
                        _ = _anomalyRepo.AddAsync(anomaly);
                    }
                }
                if (max > 0)
                {
                    decimal down = ((decimal)update.Price - (decimal)max) / (decimal)max;
                    if (down < -threshold)
                    {
                        Interlocked.Increment(ref _anomaliesDetected);
                        var anomaly = PriceAnomaly.Create(symbol, (decimal)max, update.Price, down * 100m);
                        _ = _anomalyRepo.AddAsync(anomaly);
                    }
                }
            }

            // Update symbol aggregated stats
            s.Statistics.Update(update.Price, (decimal)avg);
        }

        private int PartitionId(string symbol)
        {
            int h = symbol.GetHashCode() & 0x7fffffff;
            return h % _channels.Length;
        }

        private sealed class PartitionState
        {
            public ConcurrentDictionary<string, SymbolState> Symbols { get; } = new();

            public PartitionState(MarketDataProcessingOptions options) { }
        }

        private sealed class SymbolState
        {
            public string Symbol { get; }
            public MovingAverageBuffer MovingAverage { get; }
            public SlidingWindow Window { get; }
            public SymbolStatistics Statistics { get; }

            public SymbolState(string symbol, MarketDataProcessingOptions options)
            {
                Symbol = symbol;
                MovingAverage = new MovingAverageBuffer(Math.Max(1, options.MovingAverageWindow));
                Window = new SlidingWindow(Math.Max(1, options.SlidingWindowMilliseconds));
                Statistics = SymbolStatistics.Create(symbol);
            }
        }

        // Expose snapshots for repository
        public SymbolStatistics? TryGetSymbolStatistics(string symbol)
        {
            foreach (var p in _states)
            {
                if (p.Symbols.TryGetValue(symbol, out var s))
                    return s.Statistics.Clone();
            }
            return null;
        }

        public IEnumerable<SymbolStatistics> GetAllStatistics()
        {
            foreach (var p in _states)
            {
                foreach (var s in p.Symbols.Values)
                    yield return s.Statistics.Clone();
            }
        }
    }
}
