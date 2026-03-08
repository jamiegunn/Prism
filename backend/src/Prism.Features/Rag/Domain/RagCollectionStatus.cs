namespace Prism.Features.Rag.Domain;

/// <summary>
/// Represents the status of a RAG collection.
/// </summary>
public enum RagCollectionStatus
{
    /// <summary>The collection is ready for queries.</summary>
    Ready,

    /// <summary>Documents are being ingested and indexed.</summary>
    Indexing,

    /// <summary>An error occurred during indexing.</summary>
    Error
}
