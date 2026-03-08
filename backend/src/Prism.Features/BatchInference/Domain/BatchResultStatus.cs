namespace Prism.Features.BatchInference.Domain;

/// <summary>
/// Represents the status of an individual batch result.
/// </summary>
public enum BatchResultStatus
{
    /// <summary>The record is pending processing.</summary>
    Pending,

    /// <summary>The record was processed successfully.</summary>
    Success,

    /// <summary>The record failed and all retries exhausted.</summary>
    Failed,

    /// <summary>The record is queued for retry.</summary>
    Retry
}
