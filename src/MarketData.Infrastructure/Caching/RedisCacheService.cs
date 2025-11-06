using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace MarketData.Infrastructure.Caching;

/// <summary>
/// Service for managing distributed caching with Redis
/// </summary>
public class RedisCacheService
{
    private readonly IDistributedCache _distributedCache;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly DistributedCacheEntryOptions _defaultOptions;

    public RedisCacheService(
        IDistributedCache distributedCache,
        ILogger<RedisCacheService> logger)
    {
        _distributedCache = distributedCache ?? throw new ArgumentNullException(nameof(distributedCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _defaultOptions = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10),
            SlidingExpiration = TimeSpan.FromMinutes(2)
        };
    }

    /// <summary>
    /// Gets a cached value by key
    /// </summary>
    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            var cachedData = await _distributedCache.GetStringAsync(key, cancellationToken);

            if (string.IsNullOrEmpty(cachedData))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(cachedData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving cache key {Key}", key);
            return default;
        }
    }

    /// <summary>
    /// Sets a cached value with optional expiration
    /// </summary>
    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new DistributedCacheEntryOptions();

            if (absoluteExpiration.HasValue)
            {
                options.AbsoluteExpirationRelativeToNow = absoluteExpiration.Value;
            }
            else if (slidingExpiration.HasValue)
            {
                options.SlidingExpiration = slidingExpiration.Value;
            }
            else
            {
                options = _defaultOptions;
            }

            var serializedData = JsonSerializer.Serialize(value);
            await _distributedCache.SetStringAsync(key, serializedData, options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error setting cache key {Key}", key);
        }
    }

    /// <summary>
    /// Removes a cached value by key
    /// </summary>
    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _distributedCache.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing cache key {Key}", key);
        }
    }

    /// <summary>
    /// Gets or sets a cached value
    /// </summary>
    public async Task<T> GetOrSetAsync<T>(
        string key,
        Func<Task<T>> factory,
        TimeSpan? absoluteExpiration = null,
        TimeSpan? slidingExpiration = null,
        CancellationToken cancellationToken = default)
    {
        var cachedValue = await GetAsync<T>(key, cancellationToken);

        if (cachedValue != null)
        {
            return cachedValue;
        }

        var value = await factory();
        await SetAsync(key, value, absoluteExpiration, slidingExpiration, cancellationToken);

        return value;
    }

    /// <summary>
    /// Refreshes the sliding expiration of a cached key
    /// </summary>
    public async Task RefreshAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _distributedCache.RefreshAsync(key, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error refreshing cache key {Key}", key);
        }
    }
}
