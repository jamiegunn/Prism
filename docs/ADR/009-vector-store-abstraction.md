# ADR-009: Vector Store Abstraction for RAG

**Date:** 2026-03-05
**Status:** Accepted
**Deciders:** Project team

## Context

The RAG Workbench (Phase 4) requires vector similarity search for document retrieval. The platform needs to store embeddings, index them, and query by cosine/dot-product similarity.

Options range from using the existing PostgreSQL instance with pgvector to deploying a dedicated vector database. The decision affects infrastructure complexity, performance characteristics, and future scalability.

## Decision

Abstract the vector store behind `IVectorStore` and default to **pgvector** (PostgreSQL extension).

### Interface

```csharp
/// <summary>
/// Abstracts vector similarity search operations. Implementations include
/// pgvector (default, uses existing PostgreSQL), Qdrant, and Pinecone.
/// Features code against this interface — swap by changing config.
/// </summary>
public interface IVectorStore
{
    /// <summary>
    /// Create a named collection (logical grouping of vectors with shared schema).
    /// </summary>
    Task<Result<VectorCollection>> CreateCollectionAsync(
        string name, int dimensions, DistanceMetric metric, CancellationToken ct);

    /// <summary>
    /// Delete a collection and all its vectors.
    /// </summary>
    Task<Result> DeleteCollectionAsync(string name, CancellationToken ct);

    /// <summary>
    /// List all collections.
    /// </summary>
    Task<Result<IReadOnlyList<VectorCollection>>> ListCollectionsAsync(CancellationToken ct);

    /// <summary>
    /// Upsert vectors with metadata. Idempotent — existing IDs are overwritten.
    /// </summary>
    Task<Result> UpsertAsync(string collection, IReadOnlyList<VectorRecord> records, CancellationToken ct);

    /// <summary>
    /// Find the K nearest vectors to the query vector.
    /// </summary>
    /// <param name="collection">Collection to search.</param>
    /// <param name="queryVector">The query embedding.</param>
    /// <param name="topK">Number of results to return.</param>
    /// <param name="filter">Optional metadata filter (e.g., {"source": "paper.pdf"}).</param>
    /// <param name="ct">Cancellation token.</param>
    Task<Result<IReadOnlyList<VectorSearchResult>>> SearchAsync(
        string collection, ReadOnlyMemory<float> queryVector, int topK,
        VectorFilter? filter, CancellationToken ct);

    /// <summary>
    /// Delete vectors by ID.
    /// </summary>
    Task<Result> DeleteAsync(string collection, IReadOnlyList<string> ids, CancellationToken ct);

    /// <summary>
    /// Get collection statistics (vector count, index status, storage size).
    /// </summary>
    Task<Result<CollectionStats>> GetStatsAsync(string name, CancellationToken ct);
}

/// <summary>
/// A single vector with its ID, embedding, and arbitrary metadata.
/// </summary>
public sealed record VectorRecord(
    string Id,
    ReadOnlyMemory<float> Vector,
    Dictionary<string, object>? Metadata
);

/// <summary>
/// A search result with the matched vector, similarity score, and metadata.
/// </summary>
public sealed record VectorSearchResult(
    string Id,
    float Score,
    Dictionary<string, object>? Metadata
);

public enum DistanceMetric { Cosine, DotProduct, Euclidean }
```

### Implementations

| Provider | Config Value | Use Case |
|----------|-------------|----------|
| `PgVectorStore` | `"PgVector"` | Default — uses existing PostgreSQL with pgvector extension |
| `QdrantVectorStore` | `"Qdrant"` | Open-source, purpose-built, runs locally in Docker |
| `PineconeVectorStore` | `"Pinecone"` | Managed cloud service for production scale |

### Why pgvector as Default

- **No new infrastructure.** PostgreSQL is already in the stack. `CREATE EXTENSION vector;` is one command.
- **Research scale is fine.** pgvector handles millions of vectors efficiently with HNSW indexes. Research workloads rarely exceed this.
- **Simplified operations.** One database to back up, monitor, and manage.
- **Full SQL integration.** Can join vector search results with relational data (experiments, history records) in a single query — impossible with a separate vector DB.
- **Metadata filtering.** pgvector queries can include WHERE clauses on any PostgreSQL column — no separate metadata index needed.

### Configuration

```json
{
  "VectorStore": {
    "Provider": "PgVector",

    "PgVector": {
      "Schema": "vectors",
      "DefaultIndexType": "hnsw",
      "HnswM": 16,
      "HnswEfConstruction": 64
    },

    "Qdrant": {
      "Endpoint": "http://localhost:6333",
      "ApiKey": null
    },

    "Pinecone": {
      "ApiKey": "...",
      "Environment": "us-east-1"
    }
  }
}
```

### pgvector Implementation Notes

- Collections map to PostgreSQL tables in a `vectors` schema: `vectors.{collection_name}`
- Each table has: `id TEXT PRIMARY KEY, embedding vector({dimensions}), metadata JSONB, created_at TIMESTAMPTZ`
- HNSW index is created per collection for fast approximate nearest neighbor search
- `SearchAsync` uses `<=>` (cosine), `<#>` (dot product), or `<->` (L2) operators based on `DistanceMetric`
- Metadata filtering uses JSONB operators on the `metadata` column

## Consequences

### Positive

- Zero additional infrastructure in Phase 4 — pgvector is a Postgres extension
- Vector search results can JOIN with relational data (experiments, history, annotations)
- Feature code depends only on `IVectorStore` — backend is swappable
- Qdrant/Pinecone are available if scale demands exceed pgvector's capabilities
- Consistent with the platform's provider abstraction pattern (ADR-003, 004, 005, 006)

### Negative

- pgvector performance is good but not best-in-class for >10M vectors — Qdrant/Pinecone are faster at extreme scale
- pgvector HNSW indexes consume significant memory — large collections may pressure the database
- Metadata filtering in pgvector uses JSONB queries, which are less optimized than Qdrant's native metadata indexes
- Mixing vector workloads with OLTP queries on the same database could cause resource contention

### Neutral

- Embedding generation is NOT part of `IVectorStore` — embeddings are generated by the inference provider (`IInferenceProvider` or a dedicated embedding model) and passed to `UpsertAsync`
- Collection dimensions are fixed at creation time and must match the embedding model's output size
- `IVectorStore` does not handle chunking or document parsing — that's the RAG feature's responsibility (chunking strategies)

## Alternatives Considered

| Alternative | Pros | Cons | Why Not |
|-------------|------|------|---------|
| Qdrant as default | Purpose-built, fast, great filtering | Extra Docker container, separate backup, no SQL JOINs | Unnecessary complexity when pgvector handles research scale |
| Pinecone as default | Managed, scalable, zero-ops | Cloud-only, paid, no local option, data leaves machine | Violates local-first principle |
| ChromaDB | Simple Python API, easy setup | Python-native (HTTP API available), limited filtering | Weaker ecosystem for .NET integration |
| No abstraction (pgvector only) | Simpler, fewer interfaces | Locked to Postgres, hard to swap if scale demands it | Violates provider abstraction principle |

## References

- See `ARCHITECTURE.md` — RAG Workbench feature structure
- [pgvector documentation](https://github.com/pgvector/pgvector)
- ADR-008 — Database abstraction (pgvector shares the PostgreSQL instance)
