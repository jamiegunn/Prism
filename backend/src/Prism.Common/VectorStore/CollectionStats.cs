namespace Prism.Common.VectorStore;

/// <summary>
/// Represents statistics about a vector collection.
/// </summary>
/// <param name="Name">The name of the collection.</param>
/// <param name="VectorCount">The total number of vectors in the collection.</param>
/// <param name="Dimensions">The dimensionality of vectors.</param>
/// <param name="Metric">The distance metric used.</param>
/// <param name="SizeBytes">The estimated storage size in bytes, if available.</param>
/// <param name="IndexedCount">The number of indexed vectors, if available.</param>
public sealed record CollectionStats(
    string Name,
    long VectorCount,
    int Dimensions,
    DistanceMetric Metric,
    long? SizeBytes,
    long? IndexedCount);
