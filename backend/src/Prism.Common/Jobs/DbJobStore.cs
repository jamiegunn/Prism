using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Prism.Common.Database;
using Prism.Common.Results;

namespace Prism.Common.Jobs;

/// <summary>
/// Database-backed implementation of <see cref="IJobStore"/>.
/// Persists job state to PostgreSQL for durability across restarts.
/// </summary>
public sealed class DbJobStore : IJobStore
{
    private readonly AppDbContext _db;
    private readonly ILogger<DbJobStore> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="DbJobStore"/> class.
    /// </summary>
    /// <param name="db">The database context.</param>
    /// <param name="logger">The logger.</param>
    public DbJobStore(AppDbContext db, ILogger<DbJobStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<Result<Guid>> CreateAsync(DurableJob job, CancellationToken ct)
    {
        _db.Set<DurableJob>().Add(job);
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation("Created durable job {JobId} of type {JobType}", job.Id, job.JobType);
        return Result<Guid>.Success(job.Id);
    }

    /// <inheritdoc />
    public async Task<Result<DurableJob>> GetAsync(Guid jobId, CancellationToken ct)
    {
        DurableJob? job = await _db.Set<DurableJob>()
            .FirstOrDefaultAsync(j => j.Id == jobId, ct);

        if (job is null)
        {
            return Result<DurableJob>.Failure(Error.NotFound($"Job {jobId} not found."));
        }

        return Result<DurableJob>.Success(job);
    }

    /// <inheritdoc />
    public async Task<Result> UpdateProgressAsync(
        Guid jobId, JobStatus status, int completedItems, int failedItems, string? errorMessage, CancellationToken ct)
    {
        DurableJob? job = await _db.Set<DurableJob>().FirstOrDefaultAsync(j => j.Id == jobId, ct);

        if (job is null)
        {
            return Result.Failure(Error.NotFound($"Job {jobId} not found."));
        }

        job.Status = status;
        job.CompletedItems = completedItems;
        job.FailedItems = failedItems;
        job.ErrorMessage = errorMessage;
        job.LastHeartbeat = DateTime.UtcNow;

        if (status == JobStatus.Running && job.StartedAt is null)
        {
            job.StartedAt = DateTime.UtcNow;
        }

        if (status is JobStatus.Complete or JobStatus.Failed)
        {
            job.CompletedAt = DateTime.UtcNow;
            job.Progress = 100;
        }
        else if (job.TotalItems > 0)
        {
            job.Progress = (int)((double)completedItems / job.TotalItems * 100);
        }

        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DurableJob>> ListAsync(string? jobType, JobStatus? status, CancellationToken ct)
    {
        IQueryable<DurableJob> query = _db.Set<DurableJob>().AsNoTracking();

        if (jobType is not null)
        {
            query = query.Where(j => j.JobType == jobType);
        }

        if (status is not null)
        {
            query = query.Where(j => j.Status == status.Value);
        }

        return await query.OrderByDescending(j => j.CreatedAt).ToListAsync(ct);
    }

    /// <inheritdoc />
    public async Task<Result> HeartbeatAsync(Guid jobId, CancellationToken ct)
    {
        DurableJob? job = await _db.Set<DurableJob>().FirstOrDefaultAsync(j => j.Id == jobId, ct);

        if (job is null)
        {
            return Result.Failure(Error.NotFound($"Job {jobId} not found."));
        }

        job.LastHeartbeat = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Result.Success();
    }
}
