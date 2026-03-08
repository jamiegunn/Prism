namespace Prism.Common.Cache;

/// <summary>
/// Defines the caching service contract for the application.
/// Implementations provide different storage backends (in-memory, Redis, null/no-op).
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// Retrieves a cached value by key.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The cached value, or null if not found or expired.</returns>
    Task<T?> GetAsync<T>(string key, CancellationToken ct);

    /// <summary>
    /// Stores a value in the cache with the specified key and options.
    /// </summary>
    /// <typeparam name="T">The type of the value to cache.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="value">The value to cache.</param>
    /// <param name="options">Optional cache entry options controlling expiration and behavior.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task SetAsync<T>(string key, T value, CacheOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Removes a cached value by key.
    /// </summary>
    /// <param name="key">The cache key to remove.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RemoveAsync(string key, CancellationToken ct);

    /// <summary>
    /// Gets a cached value by key, or creates and caches it using the factory function if not found.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key.</param>
    /// <param name="factory">A function that produces the value if not found in cache.</param>
    /// <param name="options">Optional cache entry options controlling expiration and behavior.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The cached or newly created value.</returns>
    Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CacheOptions? options = null, CancellationToken ct = default);

    /// <summary>
    /// Removes all cached entries whose keys match the specified prefix.
    /// </summary>
    /// <param name="prefix">The key prefix to match for removal.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RemoveByPrefixAsync(string prefix, CancellationToken ct);
}
