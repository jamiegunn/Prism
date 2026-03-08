namespace Prism.Common.Cache;

/// <summary>
/// Options controlling cache entry behavior including expiration policies, region grouping, and tags.
/// </summary>
/// <param name="AbsoluteExpiration">The optional absolute expiration duration from the time of creation.</param>
/// <param name="SlidingExpiration">The optional sliding expiration duration that resets on each access.</param>
/// <param name="Region">The optional logical region for grouping related cache entries.</param>
/// <param name="Tags">Optional tags for categorizing cache entries, enabling bulk operations.</param>
public sealed record CacheOptions(
    TimeSpan? AbsoluteExpiration = null,
    TimeSpan? SlidingExpiration = null,
    string? Region = null,
    IReadOnlyList<string>? Tags = null)
{
    /// <summary>
    /// Default cache options with a 5-minute absolute expiration.
    /// </summary>
    public static readonly CacheOptions Default = new(AbsoluteExpiration: TimeSpan.FromMinutes(5));

    /// <summary>
    /// Cache options with a 1-hour absolute expiration for longer-lived entries.
    /// </summary>
    public static readonly CacheOptions LongLived = new(AbsoluteExpiration: TimeSpan.FromHours(1));

    /// <summary>
    /// Cache options with a 30-second absolute expiration for short-lived entries.
    /// </summary>
    public static readonly CacheOptions ShortLived = new(AbsoluteExpiration: TimeSpan.FromSeconds(30));
}
