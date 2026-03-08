namespace Prism.Common.Cache.Providers;

/// <summary>
/// A no-op implementation of <see cref="ICacheService"/> that never caches anything.
/// Used for testing scenarios and when caching is explicitly disabled.
/// </summary>
public sealed class NullCacheService : ICacheService
{
    /// <summary>
    /// Always returns default (cache miss).
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="key">The cache key (ignored).</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The default value for <typeparamref name="T"/>.</returns>
    public Task<T?> GetAsync<T>(string key, CancellationToken ct) =>
        Task.FromResult<T?>(default);

    /// <summary>
    /// Does nothing (no-op).
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The cache key (ignored).</param>
    /// <param name="value">The value (ignored).</param>
    /// <param name="options">The cache options (ignored).</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task SetAsync<T>(string key, T value, CacheOptions? options = null, CancellationToken ct = default) =>
        Task.CompletedTask;

    /// <summary>
    /// Does nothing (no-op).
    /// </summary>
    /// <param name="key">The cache key (ignored).</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task RemoveAsync(string key, CancellationToken ct) =>
        Task.CompletedTask;

    /// <summary>
    /// Always executes the factory function since nothing is cached.
    /// </summary>
    /// <typeparam name="T">The type of the value.</typeparam>
    /// <param name="key">The cache key (ignored).</param>
    /// <param name="factory">The factory function to produce the value.</param>
    /// <param name="options">The cache options (ignored).</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The value produced by the factory function.</returns>
    public Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CacheOptions? options = null, CancellationToken ct = default) =>
        factory(ct);

    /// <summary>
    /// Does nothing (no-op).
    /// </summary>
    /// <param name="prefix">The key prefix (ignored).</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A completed task.</returns>
    public Task RemoveByPrefixAsync(string prefix, CancellationToken ct) =>
        Task.CompletedTask;
}
