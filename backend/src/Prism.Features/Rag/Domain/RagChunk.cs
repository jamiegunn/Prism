using Pgvector;
using Prism.Common.Database;

namespace Prism.Features.Rag.Domain;

/// <summary>
/// Represents a text chunk from a document with its embedding vector for similarity search.
/// </summary>
public sealed class RagChunk : BaseEntity
{
    /// <summary>
    /// Gets or sets the parent document ID.
    /// </summary>
    public Guid DocumentId { get; set; }

    /// <summary>
    /// Gets or sets the text content of this chunk.
    /// </summary>
    public string Content { get; set; } = "";

    /// <summary>
    /// Gets or sets the embedding vector for similarity search.
    /// Stored as a pgvector column.
    /// </summary>
    public Vector? Embedding { get; set; }

    /// <summary>
    /// Gets or sets the zero-based order index of this chunk within its document.
    /// </summary>
    public int OrderIndex { get; set; }

    /// <summary>
    /// Gets or sets the estimated token count for this chunk.
    /// </summary>
    public int TokenCount { get; set; }

    /// <summary>
    /// Gets or sets the character offset in the original document where this chunk starts.
    /// </summary>
    public int StartOffset { get; set; }

    /// <summary>
    /// Gets or sets the character offset in the original document where this chunk ends.
    /// </summary>
    public int EndOffset { get; set; }

    /// <summary>
    /// Gets or sets optional metadata for this chunk (e.g., heading, section).
    /// Stored as JSONB.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();
}
