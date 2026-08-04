using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;

namespace Prism.Common.Jobs;

/// <summary>
/// PostgreSQL-backed implementation of <see cref="IJobLeaseStore"/>.
/// </summary>
public sealed class DbJobLeaseStore : IJobLeaseStore
{
    private readonly AppDbContext _db;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DbJobLeaseStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DbJobLeaseStore"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger instance.</param>
    /// <param name="timeProvider">
    /// Clock used for lease arithmetic. Injected so lease expiry is testable without waiting.
    /// </param>
    public DbJobLeaseStore(
        AppDbContext db,
        ILogger<DbJobLeaseStore> logger,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public async Task<DurableJob?> ClaimNextAsync(IReadOnlyList<string> jobTypes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(jobTypes);

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

        // A single statement selects, locks and marks the row. SKIP LOCKED lets a second
        // worker move past a row the first is claiming rather than serialising behind it.
        // Doing this as read-then-update would let both workers observe the same queued row.
        const string sql = """
            UPDATE jobs
            SET "Status" = {0}::varchar, "StartedAt" = COALESCE("StartedAt", {1}), "LastHeartbeat" = {1}
            WHERE "Id" = (
                SELECT "Id" FROM jobs
                WHERE "Status" = {2}::varchar
                  AND (cardinality({3}::text[]) = 0 OR "JobType" = ANY({3}::text[]))
                ORDER BY "CreatedAt"
                FOR UPDATE SKIP LOCKED
                LIMIT 1
            )
            RETURNING *;
            """;

        List<DurableJob> claimed = await _db.Set<DurableJob>()
            .FromSqlRaw(sql, nameof(JobStatus.Running), now, nameof(JobStatus.Queued), jobTypes.ToArray())
            .ToListAsync(ct);

        DurableJob? job = claimed.FirstOrDefault();

        if (job is not null)
        {
            _logger.LogDebug("Claimed job {JobId} of type {JobType}", job.Id, job.JobType);
        }

        return job;
    }

    /// <inheritdoc />
    public async Task<int> ReclaimExpiredAsync(TimeSpan leaseTimeout, CancellationToken ct)
    {
        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;
        DateTime cutoff = now - leaseTimeout;

        // Done in SQL rather than load-modify-save so that two workers running recovery at the
        // same time cannot both reclaim the same job, and so the result is not skewed by
        // whatever this context happens to have tracked.

        // Jobs out of retries are terminal. Without this an unrunnable job is reclaimed
        // forever, taking a worker slot on every pass and starving everything behind it.
        const string failExhausted = """
            UPDATE jobs
            SET "Status" = {0}::varchar,
                "ErrorMessage" = 'Abandoned by its worker and exhausted its retries.',
                "CompletedAt" = {1}
            WHERE "Status" = {2}::varchar
              AND ("LastHeartbeat" IS NULL OR "LastHeartbeat" < {3})
              AND "RetryCount" >= "MaxRetries";
            """;

        const string requeueRest = """
            UPDATE jobs
            SET "Status" = {0}::varchar,
                "RetryCount" = "RetryCount" + 1,
                "LastHeartbeat" = NULL
            WHERE "Status" = {1}::varchar
              AND ("LastHeartbeat" IS NULL OR "LastHeartbeat" < {2})
              AND "RetryCount" < "MaxRetries";
            """;

        int failed = await _db.Database.ExecuteSqlRawAsync(
            failExhausted,
            [nameof(JobStatus.Failed), now, nameof(JobStatus.Running), cutoff],
            ct);

        int requeued = await _db.Database.ExecuteSqlRawAsync(
            requeueRest,
            [nameof(JobStatus.Queued), nameof(JobStatus.Running), cutoff],
            ct);

        if (failed + requeued > 0)
        {
            _logger.LogWarning(
                "Recovered {Total} abandoned jobs ({Requeued} requeued, {Failed} failed permanently)",
                failed + requeued, requeued, failed);
        }

        return failed + requeued;
    }

    /// <inheritdoc />
    public async Task<Result<JobStatus>> CompleteAsync(Guid jobId, string? error, CancellationToken ct)
    {
        DurableJob? job = await _db.Set<DurableJob>().FirstOrDefaultAsync(j => j.Id == jobId, ct);

        if (job is null)
        {
            return Error.NotFound($"Job {jobId} not found.");
        }

        DateTime now = _timeProvider.GetUtcNow().UtcDateTime;

        if (error is null)
        {
            job.Status = JobStatus.Complete;
            job.CompletedAt = now;
            job.Progress = 100;
        }
        else if (job.RetryCount < job.MaxRetries)
        {
            job.RetryCount++;
            job.Status = JobStatus.Queued;
            job.ErrorMessage = error;
            job.LastHeartbeat = null;
        }
        else
        {
            job.Status = JobStatus.Failed;
            job.ErrorMessage = error;
            job.CompletedAt = now;
        }

        await _db.SaveChangesAsync(ct);
        return job.Status;
    }
}
