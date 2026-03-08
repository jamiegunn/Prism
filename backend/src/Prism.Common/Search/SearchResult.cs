namespace Prism.Common.Search;

/// <summary>
/// Represents a single result from a global search query.
/// </summary>
/// <param name="EntityType">The type of entity that matched (e.g., "prompt", "experiment", "dataset").</param>
/// <param name="EntityId">The unique identifier of the matching entity.</param>
/// <param name="Title">The title or name of the matching entity.</param>
/// <param name="Snippet">A text snippet showing the matching context.</param>
/// <param name="Score">The relevance score of this result (higher is more relevant).</param>
/// <param name="Metadata">Additional metadata about the matching entity.</param>
public sealed record SearchResult(
    string EntityType,
    Guid EntityId,
    string Title,
    string Snippet,
    double Score,
    Dictionary<string, string>? Metadata);
