namespace Prism.Common.Jobs;

/// <summary>
/// Represents the lifecycle status of a background job.
/// </summary>
public enum JobStatus
{
    /// <summary>The job has been enqueued and is waiting to be processed.</summary>
    Queued,

    /// <summary>The job is currently being processed.</summary>
    Running,

    /// <summary>The job completed successfully.</summary>
    Complete,

    /// <summary>The job failed during processing.</summary>
    Failed
}
