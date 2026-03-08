using Prism.Common.Results;

namespace Prism.Common.Jobs;

/// <summary>
/// Defines the job queue contract for enqueuing and processing background work items.
/// </summary>
public interface IJobQueue
{
    /// <summary>
    /// Enqueues a work item for asynchronous processing.
    /// </summary>
    /// <typeparam name="T">The type of the work item payload.</typeparam>
    /// <param name="item">The work item to enqueue.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the unique job identifier.</returns>
    Task<Result<Guid>> EnqueueAsync<T>(T item, CancellationToken ct) where T : class;

    /// <summary>
    /// Dequeues the next available work item for processing. Blocks until an item is available.
    /// </summary>
    /// <typeparam name="T">The type of the work item payload.</typeparam>
    /// <param name="ct">A token to cancel the wait.</param>
    /// <returns>A result containing the dequeued work item.</returns>
    Task<Result<T>> DequeueAsync<T>(CancellationToken ct) where T : class;

    /// <summary>
    /// Gets the current status of a previously enqueued job.
    /// </summary>
    /// <param name="jobId">The unique identifier of the job.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>A result containing the current job status.</returns>
    Task<Result<JobStatus>> GetStatusAsync(Guid jobId, CancellationToken ct);
}
