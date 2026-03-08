namespace Prism.Common.VectorStore;

/// <summary>
/// Represents a single result from a vector similarity search.
/// </summary>
/// <param name="Id">The identifier of the matched vector record.</param>
/// <param name="Score">The similarity score (higher is more similar for cosine/dot product).</param>
/// <param name="Metadata">The metadata associated with the matched record.</param>
public sealed record VectorSearchResult(
    string Id,
    double Score,
    Dictionary<string, string> Metadata);
