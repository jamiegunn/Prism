using Prism.Common.Results;

namespace Prism.Common.Jobs;

/// <summary>
/// Atomic claim and lease-recovery operations that let several workers share a job queue
/// without executing the same job twice or losing a job when a worker dies.
/// </summary>
/// <remarks>
/// <para>
/// The pre-existing store could create, read and update jobs, but nothing could take a job.
/// Without an atomic claim, two workers racing on the same row both see it queued and both
/// run it; without lease recovery, a worker that crashes mid-job leaves that job marked
/// running forever with no one working on it.
/// </para>
/// <para>
/// Claiming uses <c>FOR UPDATE SKIP LOCKED</c>, so concurrent workers step over rows their
/// peers are claiming rather than blocking on them.
/// </para>
/// </remarks>
public interface IJobLeaseStore
{
    /// <summary>
    /// Atomically claims the oldest queued job of one of the given types and marks it running.
    /// </summary>
    /// <param name="jobTypes">Job types this worker can execute. Empty means any type.</param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The claimed job, or <see langword="null"/> when nothing is available.</returns>
    Task<DurableJob?> ClaimNextAsync(IReadOnlyList<string> jobTypes, CancellationToken ct);

    /// <summary>
    /// Returns jobs whose lease has expired to the queue, or fails them when their retry
    /// budget is exhausted.
    /// </summary>
    /// <param name="leaseTimeout">
    /// How long a running job may go without a heartbeat before it is presumed abandoned.
    /// </param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The number of jobs recovered.</returns>
    Task<int> ReclaimExpiredAsync(TimeSpan leaseTimeout, CancellationToken ct);

    /// <summary>
    /// Records the outcome of a finished job, scheduling a retry when one is warranted.
    /// </summary>
    /// <param name="jobId">The job identifier.</param>
    /// <param name="error">
    /// The failure, or <see langword="null"/> when the job succeeded.
    /// </param>
    /// <param name="ct">A token to cancel the operation.</param>
    /// <returns>The status the job now holds.</returns>
    Task<Result<JobStatus>> CompleteAsync(Guid jobId, string? error, CancellationToken ct);
}
