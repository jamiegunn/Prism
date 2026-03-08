using System.Collections.Concurrent;
using Microsoft.Extensions.Caching.Memory;

namespace Prism.Common.Cache.Providers;

/// <summary>
/// In-memory implementation of <see cref="ICacheService"/> using <see cref="IMemoryCache"/>.
/// Suitable for single-instance deployments (Phase 1-2). Tracks keys for prefix-based removal.
/// </summary>
public sealed class InMemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;
    private readonly ConcurrentDictionary<string, byte> _keys = new();
    private readonly ILogger<InMemoryCacheService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="InMemoryCacheService"/> class.
    /// </summary>
    /// <param name="cache">The underlying memory cache.</param>
    /// <param name="logger">The logger instance.</param>
    public InMemoryCacheService(IMemoryCache cache, ILogger<InMemoryCacheService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Retrieves a cached value by key from the in-memory cache.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The cached value, or default if not found.</returns>
    public Task<T?> GetAsync<T>(string key, CancellationToken ct)
    {
        bool found = _cache.TryGetValue(key, out T? value);
        _logger.LogDebug("Cache {CacheResult} for key {CacheKey}", found ? "hit" : "miss", key);
        return Task.FromResult(value);
    }

    /// <summary>
    /// Stores a value in the in-memory cache with the specified options.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="options">Optional cache entry options.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task SetAsync<T>(string key, T value, CacheOptions? options = null, CancellationToken ct = default)
    {
        MemoryCacheEntryOptions entryOptions = ToMemoryCacheOptions(options);

        entryOptions.RegisterPostEvictionCallback((evictedKey, _, _, _) =>
        {
            if (evictedKey is string keyStr)
            {
                _keys.TryRemove(keyStr, out _);
            }
        });

        _cache.Set(key, value, entryOptions);
        _keys.TryAdd(key, 0);

        _logger.LogDebug("Cached value for key {CacheKey}", key);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes a cached value by key from the in-memory cache.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task RemoveAsync(string key, CancellationToken ct)
    {
        _cache.Remove(key);
        _keys.TryRemove(key, out _);
        _logger.LogDebug("Removed cache key {CacheKey}", key);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Gets a cached value or creates and caches it using the factory function.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">A function that produces the value if not found in cache.</param>
    /// <param name="options">Optional cache entry options.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The cached or newly created value.</returns>
    public async Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CacheOptions? options = null, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(key, out T? cachedValue) && cachedValue is not null)
        {
            _logger.LogDebug("Cache hit for key {CacheKey}", key);
            return cachedValue;
        }

        _logger.LogDebug("Cache miss for key {CacheKey}, executing factory", key);
        T value = await factory(ct);
        await SetAsync(key, value, options, ct);
        return value;
    }

    /// <summary>
    /// Removes all cached entries whose keys start with the specified prefix.
    /// </summary>
    /// <param name="prefix">The key prefix to match.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct)
    {
        List<string> keysToRemove = _keys.Keys
            .Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (string key in keysToRemove)
        {
            _cache.Remove(key);
            _keys.TryRemove(key, out _);
        }

        _logger.LogDebug("Removed {Count} cache entries with prefix {Prefix}", keysToRemove.Count, prefix);
        return Task.CompletedTask;
    }

    private static MemoryCacheEntryOptions ToMemoryCacheOptions(CacheOptions? options)
    {
        MemoryCacheEntryOptions entryOptions = new();

        if (options is null)
        {
            entryOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
            return entryOptions;
        }

        if (options.AbsoluteExpiration.HasValue)
        {
            entryOptions.AbsoluteExpirationRelativeToNow = options.AbsoluteExpiration.Value;
        }

        if (options.SlidingExpiration.HasValue)
        {
            entryOptions.SlidingExpiration = options.SlidingExpiration.Value;
        }

        if (!options.AbsoluteExpiration.HasValue && !options.SlidingExpiration.HasValue)
        {
            entryOptions.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5);
        }

        return entryOptions;
    }
}
