namespace Prism.Common.Jobs;

/// <summary>
/// Executes one kind of background job.
/// </summary>
/// <remarks>
/// Implementations must be idempotent. Delivery is at-least-once: a worker that dies after
/// finishing the work but before recording completion leaves the job to be reclaimed and run
/// again, and no amount of care in the queue can remove that window.
/// </remarks>
public interface IJobHandler
{
    /// <summary>
    /// Gets the <see cref="DurableJob.JobType"/> this handler executes.
    /// </summary>
    string JobType { get; }

    /// <summary>
    /// Performs the work.
    /// </summary>
    /// <param name="job">The claimed job, including its parameters.</param>
    /// <param name="ct">
    /// Cancelled when the host is shutting down. Handlers should honour it promptly; work left
    /// incomplete is reclaimed and retried rather than lost.
    /// </param>
    /// <returns>A task that completes when the work is done.</returns>
    Task ExecuteAsync(DurableJob job, CancellationToken ct);
}
