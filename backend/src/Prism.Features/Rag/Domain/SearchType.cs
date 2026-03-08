namespace Prism.Features.Rag.Domain;

/// <summary>
/// The type of search to perform against a RAG collection.
/// </summary>
public enum SearchType
{
    /// <summary>Vector similarity search using embeddings.</summary>
    Vector,

    /// <summary>BM25 full-text search using PostgreSQL tsvector.</summary>
    Bm25,

    /// <summary>Hybrid search combining vector similarity and BM25 with weighted scoring.</summary>
    Hybrid
}
