using System.Text.Json;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Prism.Common.Cache.Providers;

/// <summary>
/// Redis implementation of <see cref="ICacheService"/> using StackExchange.Redis.
/// Suitable for multi-instance deployments and persistent caching.
/// </summary>
public sealed class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;
    private readonly JsonSerializerOptions _jsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>
    /// Initializes a new instance of the <see cref="RedisCacheService"/> class.
    /// </summary>
    /// <param name="redis">The Redis connection multiplexer.</param>
    /// <param name="logger">The logger instance.</param>
    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        IDatabase db = _redis.GetDatabase();
        RedisValue value = await db.StringGetAsync(key);

        if (value.IsNullOrEmpty)
        {
            _logger.LogDebug("Cache miss for key {CacheKey}", key);
            return default;
        }

        _logger.LogDebug("Cache hit for key {CacheKey}", key);
        return JsonSerializer.Deserialize<T>(value!, _jsonOptions);
    }

    /// <inheritdoc />
    public async Task SetAsync<T>(string key, T value, CacheOptions? options = null, CancellationToken ct = default)
    {
        IDatabase db = _redis.GetDatabase();
        string json = JsonSerializer.Serialize(value, _jsonOptions);

        TimeSpan? expiry = GetExpiry(options);
        await db.StringSetAsync(key, json, new Expiration(expiry ?? TimeSpan.FromMinutes(5)));

        _logger.LogDebug("Cached value for key {CacheKey} with expiry {Expiry}", key, expiry);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(string key, CancellationToken ct)
    {
        IDatabase db = _redis.GetDatabase();
        await db.KeyDeleteAsync(key);
        _logger.LogDebug("Removed cache key {CacheKey}", key);
    }

    /// <inheritdoc />
    public async Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CacheOptions? options = null, CancellationToken ct = default)
    {
        T? cached = await GetAsync<T>(key, ct);
        if (cached is not null)
        {
            return cached;
        }

        _logger.LogDebug("Cache miss for key {CacheKey}, executing factory", key);
        T value = await factory(ct);
        await SetAsync(key, value, options, ct);
        return value;
    }

    /// <inheritdoc />
    public async Task RemoveByPrefixAsync(string prefix, CancellationToken ct)
    {
        IDatabase db = _redis.GetDatabase();
        IServer server = _redis.GetServers().First();

        int count = 0;
        await foreach (RedisKey key in server.KeysAsync(pattern: $"{prefix}*"))
        {
            await db.KeyDeleteAsync(key);
            count++;
        }

        _logger.LogDebug("Removed {Count} cache entries with prefix {Prefix}", count, prefix);
    }

    private static TimeSpan? GetExpiry(CacheOptions? options)
    {
        if (options is null)
        {
            return TimeSpan.FromMinutes(5);
        }

        return options.AbsoluteExpiration ?? options.SlidingExpiration ?? TimeSpan.FromMinutes(5);
    }
}
