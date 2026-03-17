using Prism.Common.Database;

namespace Prism.Features.Rag.Domain;

/// <summary>
/// Persists the trace of a RAG pipeline execution: the query, retrieved chunks,
/// assembled context, generated response, and citations.
/// </summary>
public sealed class RagTrace : BaseEntity
{
    /// <summary>
    /// Gets or sets the collection this trace belongs to.
    /// </summary>
    public Guid CollectionId { get; set; }

    /// <summary>
    /// Gets or sets the user query that initiated the RAG pipeline.
    /// </summary>
    public string Query { get; set; } = "";

    /// <summary>
    /// Gets or sets the search type used (Vector, BM25, Hybrid).
    /// </summary>
    public string SearchType { get; set; } = "Vector";

    /// <summary>
    /// Gets or sets the number of chunks retrieved.
    /// </summary>
    public int RetrievedChunkCount { get; set; }

    /// <summary>
    /// Gets or sets the retrieved chunk IDs and scores as serialized JSON.
    /// </summary>
    public string RetrievedChunksJson { get; set; } = "[]";

    /// <summary>
    /// Gets or sets the assembled context sent to the model.
    /// </summary>
    public string AssembledContext { get; set; } = "";

    /// <summary>
    /// Gets or sets the generated response.
    /// </summary>
    public string? GeneratedResponse { get; set; }

    /// <summary>
    /// Gets or sets the model used for generation.
    /// </summary>
    public string? Model { get; set; }

    /// <summary>
    /// Gets or sets the latency in milliseconds.
    /// </summary>
    public long LatencyMs { get; set; }

    /// <summary>
    /// Gets or sets the total tokens used.
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// Gets or sets whether the pipeline completed successfully.
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Gets or sets the error message if failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
