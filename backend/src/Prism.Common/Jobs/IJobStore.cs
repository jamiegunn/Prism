using Prism.Common.Results;

namespace Prism.Common.Jobs;

/// <summary>
/// Persistent store for durable job records. Enables job state to survive restarts.
/// </summary>
public interface IJobStore
{
    /// <summary>
    /// Creates a new job record in the store.
    /// </summary>
    /// <param name="job">The job to persist.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the persisted job ID.</returns>
    Task<Result<Guid>> CreateAsync(DurableJob job, CancellationToken ct);

    /// <summary>
    /// Gets a job by its ID.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the job, or NotFound.</returns>
    Task<Result<DurableJob>> GetAsync(Guid jobId, CancellationToken ct);

    /// <summary>
    /// Updates job progress and status.
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="status">The new status.</param>
    /// <param name="completedItems">The number of completed items.</param>
    /// <param name="failedItems">The number of failed items.</param>
    /// <param name="errorMessage">Optional error message.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success.</returns>
    Task<Result> UpdateProgressAsync(Guid jobId, JobStatus status, int completedItems, int failedItems, string? errorMessage, CancellationToken ct);

    /// <summary>
    /// Lists jobs with optional type and status filters.
    /// </summary>
    /// <param name="jobType">Optional job type filter.</param>
    /// <param name="status">Optional status filter.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A list of matching jobs.</returns>
    Task<IReadOnlyList<DurableJob>> ListAsync(string? jobType, JobStatus? status, CancellationToken ct);

    /// <summary>
    /// Records a heartbeat for a running job (lease renewal).
    /// </summary>
    /// <param name="jobId">The job ID.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result indicating success.</returns>
    Task<Result> HeartbeatAsync(Guid jobId, CancellationToken ct);
}
