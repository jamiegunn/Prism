namespace Prism.Features.Rag.Domain;

/// <summary>
/// Distance metric for vector similarity search in a RAG collection.
/// </summary>
public enum DistanceMetricType
{
    /// <summary>Cosine similarity — best for normalized text embeddings.</summary>
    Cosine,

    /// <summary>Euclidean (L2) distance.</summary>
    Euclidean,

    /// <summary>Inner (dot) product similarity.</summary>
    InnerProduct
}
