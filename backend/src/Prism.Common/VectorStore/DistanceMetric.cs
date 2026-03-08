namespace Prism.Common.VectorStore;

/// <summary>
/// Specifies the distance metric used for vector similarity search.
/// </summary>
public enum DistanceMetric
{
    /// <summary>Cosine similarity (1 - cosine distance). Most common for text embeddings.</summary>
    Cosine,

    /// <summary>Euclidean (L2) distance. Suitable when magnitude matters.</summary>
    Euclidean,

    /// <summary>Dot product similarity. Fast and suitable for normalized vectors.</summary>
    DotProduct
}
