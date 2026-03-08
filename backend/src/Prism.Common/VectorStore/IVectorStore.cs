using Prism.Common.Results;

namespace Prism.Common.VectorStore;

/// <summary>
/// Defines the vector store contract for embedding storage and similarity search.
/// Implementations provide different backends (pgvector, Qdrant, Pinecone).
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Creates a new vector collection with the specified dimensions and distance metric.
    /// </summary>
    /// <param name="name">The name of the collection to create.</param>
    /// <param name="dimensions">The dimensionality of vectors in the collection.</param>
    /// <param name="metric">The distance metric for similarity search.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> CreateCollectionAsync(string name, int dimensions, DistanceMetric metric, CancellationToken ct);

    /// <summary>
    /// Deletes a vector collection and all its vectors.
    /// </summary>
    /// <param name="name">The name of the collection to delete.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> DeleteCollectionAsync(string name, CancellationToken ct);

    /// <summary>
    /// Inserts or updates vector records in the specified collection.
    /// </summary>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="records">The vector records to upsert.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> UpsertAsync(string collectionName, IReadOnlyList<VectorRecord> records, CancellationToken ct);

    /// <summary>
    /// Performs a similarity search in the specified collection.
    /// </summary>
    /// <param name="collectionName">The name of the collection to search.</param>
    /// <param name="queryVector">The query embedding vector.</param>
    /// <param name="topK">The maximum number of results to return.</param>
    /// <param name="filter">Optional metadata filter to narrow results.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the search results ordered by similarity.</returns>
    Task<Result<IReadOnlyList<VectorSearchResult>>> SearchAsync(
        string collectionName,
        ReadOnlyMemory<float> queryVector,
        int topK,
        VectorFilter? filter,
        CancellationToken ct);

    /// <summary>
    /// Deletes vector records by their identifiers from the specified collection.
    /// </summary>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="ids">The identifiers of the records to delete.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success or failure.</returns>
    Task<Result> DeleteAsync(string collectionName, IReadOnlyList<string> ids, CancellationToken ct);

    /// <summary>
    /// Gets statistics about a vector collection.
    /// </summary>
    /// <param name="collectionName">The name of the collection.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing collection statistics.</returns>
    Task<Result<CollectionStats>> GetStatsAsync(string collectionName, CancellationToken ct);
}
