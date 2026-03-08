namespace Prism.Features.BatchInference.Domain;

/// <summary>
/// Represents the execution status of a batch inference job.
/// </summary>
public enum BatchJobStatus
{
    /// <summary>The job is queued for processing.</summary>
    Queued,

    /// <summary>The job is currently running.</summary>
    Running,

    /// <summary>The job has been paused.</summary>
    Paused,

    /// <summary>The job completed successfully.</summary>
    Completed,

    /// <summary>The job failed with an error.</summary>
    Failed,

    /// <summary>The job was cancelled by the user.</summary>
    Cancelled
}
