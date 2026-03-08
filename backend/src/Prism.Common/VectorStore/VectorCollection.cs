namespace Prism.Common.VectorStore;

/// <summary>
/// Represents a named collection of vectors in the vector store.
/// </summary>
/// <param name="Name">The name of the collection.</param>
/// <param name="Dimensions">The dimensionality of vectors in this collection.</param>
/// <param name="Metric">The distance metric used for similarity search.</param>
/// <param name="VectorCount">The current number of vectors stored in the collection.</param>
public sealed record VectorCollection(
    string Name,
    int Dimensions,
    DistanceMetric Metric,
    long VectorCount);
