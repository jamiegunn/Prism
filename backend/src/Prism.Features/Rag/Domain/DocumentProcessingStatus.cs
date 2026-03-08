namespace Prism.Features.Rag.Domain;

/// <summary>
/// Represents the processing status of an ingested document.
/// </summary>
public enum DocumentProcessingStatus
{
    /// <summary>Waiting to be processed.</summary>
    Pending,

    /// <summary>Currently being chunked and embedded.</summary>
    Processing,

    /// <summary>Successfully processed with all chunks embedded.</summary>
    Completed,

    /// <summary>Processing failed.</summary>
    Failed
}
