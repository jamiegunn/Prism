using Prism.Common.Database;

namespace Prism.Common.Jobs;

/// <summary>
/// A durable job record persisted to the database.
/// Survives application restarts and provides progress tracking.
/// </summary>
public sealed class DurableJob : BaseEntity
{
    /// <summary>
    /// Gets or sets the job type discriminator (e.g., "batch_inference", "evaluation", "rag_ingest").
    /// </summary>
    public string JobType { get; set; } = "";

    /// <summary>
    /// Gets or sets the current job status.
    /// </summary>
    public JobStatus Status { get; set; } = JobStatus.Queued;

    /// <summary>
    /// Gets or sets the job parameters as serialized JSON.
    /// </summary>
    public string ParametersJson { get; set; } = "{}";

    /// <summary>
    /// Gets or sets the progress percentage (0-100).
    /// </summary>
    public int Progress { get; set; }

    /// <summary>
    /// Gets or sets the total number of items to process.
    /// </summary>
    public int TotalItems { get; set; }

    /// <summary>
    /// Gets or sets the number of completed items.
    /// </summary>
    public int CompletedItems { get; set; }

    /// <summary>
    /// Gets or sets the number of failed items.
    /// </summary>
    public int FailedItems { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of retries allowed.
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Gets or sets the current retry count.
    /// </summary>
    public int RetryCount { get; set; }

    /// <summary>
    /// Gets or sets the error message if the job failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets when the job started executing.
    /// </summary>
    public DateTime? StartedAt { get; set; }

    /// <summary>
    /// Gets or sets when the job completed (successfully or with failure).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// Gets or sets the last heartbeat timestamp (for lease management).
    /// </summary>
    public DateTime? LastHeartbeat { get; set; }

    /// <summary>
    /// Gets or sets the optional workspace ID.
    /// </summary>
    public Guid? WorkspaceId { get; set; }

    /// <summary>
    /// Gets or sets the optional project ID.
    /// </summary>
    public Guid? ProjectId { get; set; }
}
