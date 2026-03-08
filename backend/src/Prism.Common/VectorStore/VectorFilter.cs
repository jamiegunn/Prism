namespace Prism.Common.VectorStore;

/// <summary>
/// Represents a metadata filter for vector search queries.
/// Filters restrict search results to vectors whose metadata matches the specified conditions.
/// </summary>
public sealed record VectorFilter
{
    /// <summary>
    /// Gets or initializes the metadata key-value pairs that must match exactly.
    /// </summary>
    public Dictionary<string, string> ExactMatch { get; init; } = new();

    /// <summary>
    /// Creates an empty filter that matches all records.
    /// </summary>
    /// <returns>A <see cref="VectorFilter"/> with no conditions.</returns>
    public static VectorFilter None() => new();

    /// <summary>
    /// Creates a filter requiring an exact match on a single metadata field.
    /// </summary>
    /// <param name="key">The metadata key to filter on.</param>
    /// <param name="value">The required value.</param>
    /// <returns>A <see cref="VectorFilter"/> with the specified condition.</returns>
    public static VectorFilter ByField(string key, string value) =>
        new() { ExactMatch = new Dictionary<string, string> { { key, value } } };

    /// <summary>
    /// Gets a value indicating whether this filter has any conditions.
    /// </summary>
    public bool HasConditions => ExactMatch.Count > 0;
}
