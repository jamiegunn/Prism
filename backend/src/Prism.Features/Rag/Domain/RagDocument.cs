using Prism.Common.Database;

namespace Prism.Features.Rag.Domain;

/// <summary>
/// Represents an ingested document within a RAG collection.
/// </summary>
public sealed class RagDocument : BaseEntity
{
    /// <summary>
    /// Gets or sets the parent collection ID.
    /// </summary>
    public Guid CollectionId { get; set; }

    /// <summary>
    /// Gets or sets the original filename.
    /// </summary>
    public string Filename { get; set; } = "";

    /// <summary>
    /// Gets or sets the content type (text/plain, text/markdown, text/html, application/pdf).
    /// </summary>
    public string ContentType { get; set; } = "";

    /// <summary>
    /// Gets or sets the file size in bytes.
    /// </summary>
    public long SizeBytes { get; set; }

    /// <summary>
    /// Gets or sets the number of chunks produced from this document.
    /// </summary>
    public int ChunkCount { get; set; }

    /// <summary>
    /// Gets or sets the total character count of extracted text.
    /// </summary>
    public int CharacterCount { get; set; }

    /// <summary>
    /// Gets or sets optional metadata about the document as key-value pairs.
    /// Stored as JSONB.
    /// </summary>
    public Dictionary<string, string> Metadata { get; set; } = new();

    /// <summary>
    /// Gets or sets the processing status (pending, processing, completed, failed).
    /// </summary>
    public DocumentProcessingStatus Status { get; set; } = DocumentProcessingStatus.Pending;

    /// <summary>
    /// Gets or sets the error message if processing failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets the chunks derived from this document.
    /// </summary>
    public List<RagChunk> Chunks { get; set; } = [];
}
