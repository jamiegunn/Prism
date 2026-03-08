namespace Prism.Common.Cache;

/// <summary>
/// Static helper for building namespaced, consistent cache keys.
/// Ensures cache keys follow a predictable pattern to avoid collisions between features.
/// </summary>
public static class CacheKeyBuilder
{
    private const string Separator = ":";

    /// <summary>
    /// Builds a cache key from a feature name and key segments.
    /// </summary>
    /// <param name="feature">The feature or module name (e.g., "playground", "models").</param>
    /// <param name="segments">Additional key segments to append.</param>
    /// <returns>A namespaced cache key in the format "feature:segment1:segment2".</returns>
    /// <example>
    /// CacheKeyBuilder.Build("models", "info", "llama-3") returns "models:info:llama-3"
    /// </example>
    public static string Build(string feature, params string[] segments)
    {
        if (segments.Length == 0)
        {
            return feature.ToLowerInvariant();
        }

        return string.Concat(
            feature.ToLowerInvariant(),
            Separator,
            string.Join(Separator, segments));
    }

    /// <summary>
    /// Builds a cache key for a specific entity by its identifier.
    /// </summary>
    /// <param name="feature">The feature or module name.</param>
    /// <param name="entityType">The entity type name.</param>
    /// <param name="id">The entity identifier.</param>
    /// <returns>A cache key in the format "feature:entityType:id".</returns>
    /// <example>
    /// CacheKeyBuilder.ForEntity("playground", "session", sessionId) returns "playground:session:{id}"
    /// </example>
    public static string ForEntity(string feature, string entityType, Guid id) =>
        Build(feature, entityType, id.ToString());

    /// <summary>
    /// Builds a cache key for a list or collection within a feature.
    /// </summary>
    /// <param name="feature">The feature or module name.</param>
    /// <param name="listName">The name of the list or collection.</param>
    /// <returns>A cache key in the format "feature:list:listName".</returns>
    public static string ForList(string feature, string listName) =>
        Build(feature, "list", listName);

    /// <summary>
    /// Returns the prefix for all cache keys belonging to a feature, suitable for bulk invalidation.
    /// </summary>
    /// <param name="feature">The feature or module name.</param>
    /// <returns>A prefix string in the format "feature:".</returns>
    public static string PrefixFor(string feature) =>
        string.Concat(feature.ToLowerInvariant(), Separator);
}
