namespace MarketData.Infrastructure.Options
{
    /// <summary>
    /// Strongly-typed options controlling the processing engine behavior.
    /// </summary>
    public sealed class MarketDataProcessingOptions
    {
        public int Partitions { get; set; } = 0;
        public int ChannelCapacity { get; set; } = 100_000;
        public int MovingAverageWindow { get; set; } = 64;
        public decimal AnomalyThresholdPercent { get; set; } = 2.0m;
        public int SlidingWindowMilliseconds { get; set; } = 1000;
        public int RecentAnomaliesCapacity { get; set; } = 10_000;

        public SimulationOptions Simulation { get; set; } = new SimulationOptions();

        public sealed class SimulationOptions
        {
            public bool Enabled { get; set; } = true;
            public string[] Symbols { get; set; } = new[] { "AAPL", "GOOG", "MSFT", "EURUSD", "BTCUSD" };
            public int TicksPerSecond { get; set; } = 2000;
            public decimal InitialPrice { get; set; } = 100m;
            public decimal MaxJitterPercent { get; set; } = 0.01m;
        }
    }
}
