namespace MarketData.Infrastructure.Caching;

/// <summary>
/// Centralized cache key management
/// </summary>
public static class CacheKeys
{
    private const string Prefix = "MarketData:";

    public static class Statistics
    {
        private const string StatsPrefix = Prefix + "Stats:";

        public static string BySymbol(string symbol) => $"{StatsPrefix}Symbol:{symbol}";
        public static string All() => $"{StatsPrefix}All";
    }

    public static class Anomalies
    {
        private const string AnomalyPrefix = Prefix + "Anomaly:";

        public static string Recent(int take, string? symbol = null) =>
            symbol != null
                ? $"{AnomalyPrefix}Recent:{take}:Symbol:{symbol}"
                : $"{AnomalyPrefix}Recent:{take}";

        public static string Count(string? symbol = null) =>
            symbol != null
                ? $"{AnomalyPrefix}Count:Symbol:{symbol}"
                : $"{AnomalyPrefix}Count";
    }

    public static class PriceUpdates
    {
        private const string PricePrefix = Prefix + "Price:";

        public static string BySymbol(string symbol, int take) => $"{PricePrefix}Symbol:{symbol}:Take:{take}";
        public static string Latest(string symbol) => $"{PricePrefix}Latest:Symbol:{symbol}";
    }

    public static class Users
    {
        private const string UserPrefix = Prefix + "User:";

        public static string ById(Guid userId) => $"{UserPrefix}Id:{userId}";
        public static string ByUsername(string username) => $"{UserPrefix}Username:{username}";
        public static string ByEmail(string email) => $"{UserPrefix}Email:{email}";
    }
}
