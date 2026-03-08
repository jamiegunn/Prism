using Prism.Common.Database;

namespace Prism.Features.Rag.Domain;

/// <summary>
/// Aggregate root representing a RAG collection that groups documents and their chunked embeddings.
/// </summary>
public sealed class RagCollection : BaseEntity
{
    /// <summary>
    /// Gets or sets the optional project this collection belongs to.
    /// </summary>
    public Guid? ProjectId { get; set; }

    /// <summary>
    /// Gets or sets the display name of the collection.
    /// </summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Gets or sets the optional description.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the embedding model used for this collection (e.g., "text-embedding-3-small").
    /// </summary>
    public string EmbeddingModel { get; set; } = "";

    /// <summary>
    /// Gets or sets the dimensionality of the embedding vectors.
    /// </summary>
    public int Dimensions { get; set; }

    /// <summary>
    /// Gets or sets the distance metric for similarity search.
    /// </summary>
    public DistanceMetricType DistanceMetric { get; set; } = DistanceMetricType.Cosine;

    /// <summary>
    /// Gets or sets the chunking strategy name (fixed, sentence, recursive).
    /// </summary>
    public string ChunkingStrategy { get; set; } = "recursive";

    /// <summary>
    /// Gets or sets the target chunk size in tokens.
    /// </summary>
    public int ChunkSize { get; set; } = 512;

    /// <summary>
    /// Gets or sets the chunk overlap in tokens.
    /// </summary>
    public int ChunkOverlap { get; set; } = 50;

    /// <summary>
    /// Gets or sets the total number of documents in this collection.
    /// </summary>
    public int DocumentCount { get; set; }

    /// <summary>
    /// Gets or sets the total number of chunks across all documents.
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// Gets or sets the status of the collection (ready, indexing, error).
    /// </summary>
    public RagCollectionStatus Status { get; set; } = RagCollectionStatus.Ready;

    /// <summary>
    /// Gets the documents belonging to this collection.
    /// </summary>
    public List<RagDocument> Documents { get; set; } = [];
}
