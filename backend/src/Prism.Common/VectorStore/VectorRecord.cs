namespace Prism.Common.VectorStore;

/// <summary>
/// Represents a vector record with an embedding and associated metadata for storage and retrieval.
/// </summary>
/// <param name="Id">The unique identifier for this vector record.</param>
/// <param name="Embedding">The embedding vector as a read-only memory of floats.</param>
/// <param name="Metadata">Key-value metadata associated with this vector for filtering.</param>
public sealed record VectorRecord(
    string Id,
    ReadOnlyMemory<float> Embedding,
    Dictionary<string, string> Metadata);
